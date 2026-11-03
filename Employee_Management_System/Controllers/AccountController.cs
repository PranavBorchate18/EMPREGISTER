
using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Employee_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;

namespace Employee_Management_System.Controllers
{
    public class AccountController : Controller
    {
        private const string TakeoverChallengeKey = "EMS_TakeoverChallenge";
        private const string TakeoverLoginIdKey = "EMS_TakeoverLoginId";
        private const string TakeoverExpectedTokenKey = "EMS_TakeoverExpectedToken";
        private const string TakeoverWorkingDateKey = "EMS_TakeoverWorkingDate";
        private const string TakeoverExpiryKey = "EMS_TakeoverExpiryUtc";

        private readonly ApplicationDbContext _context;
        private readonly ActiveSessionService _activeSessions;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            ApplicationDbContext context,
            ActiveSessionService activeSessions,
            ILogger<AccountController> logger)
        {
            _context = context;
            _activeSessions = activeSessions;
            _logger = logger;
        }

        private string GetOrganisationLoginKey(string loginId)
        {
            string organisationCode =
                HttpContext.Session.GetString("OrganisationCode") ?? "";

            return $"{organisationCode}:{loginId}";
        }

        private bool IsOrganisationSelected()
        {
            return HttpContext.Session.GetString("OrganisationCode") != null;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(
            bool tabEntry = false,
            bool tabLost = false)
        {
            if (!IsOrganisationSelected())
            {
                return RedirectToAction("Index", "Organisation");
            }

            string? loginId =
                HttpContext.Session.GetString("LoginId");

            string? token =
                HttpContext.Session.GetString("ActiveSessionToken");

            string sessionKey = string.IsNullOrWhiteSpace(loginId)
                ? ""
                : GetOrganisationLoginKey(loginId);

            bool valid =
                !string.IsNullOrWhiteSpace(loginId) &&
                !string.IsNullOrWhiteSpace(token) &&
                _activeSessions.IsCurrent(sessionKey, token);

            // Same browser duplicate tab handling
            if (tabEntry && valid)
            {
                var activeTab =
                    _activeSessions.GetActiveTab(sessionKey, token!);

                if (activeTab == null)
                {
                    return RedirectToAction("Index", "Home");
                }

                ViewBag.TabEntry = true;

                ViewBag.TabExistingSession =
                    _activeSessions.Get(sessionKey);

                ViewBag.ExpectedOldTabId = activeTab.TabId;

                ViewBag.WorkingDate =
                    HttpContext.Session.GetString("WorkingDate")
                    ?? DateTime.Today.ToString("yyyy-MM-dd");

                return View(new LoginViewModel());
            }

            if (tabLost)
            {
                ViewBag.TabLost = true;

                ViewBag.WorkingDate =
                    HttpContext.Session.GetString("WorkingDate")
                    ?? DateTime.Today.ToString("yyyy-MM-dd");

                return View(new LoginViewModel());
            }

            if (valid)
            {
                return RedirectToAction("Index", "Home");
            }

            // Remove login information only.
            // Keep selected Organisation information.
            ClearLoginSession();
            ClearTakeoverChallenge();

            ViewBag.WorkingDate =
                DateTime.Today.ToString("yyyy-MM-dd");

            return View(new LoginViewModel());
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model,
            DateTime? workingDate)
        {
            if (!IsOrganisationSelected())
            {
                return RedirectToAction("Index", "Organisation");
            }

            ViewBag.WorkingDate =
                workingDate?.ToString("yyyy-MM-dd")
                ?? DateTime.Today.ToString("yyyy-MM-dd");

            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "Login validation failed: {Errors}",
                    string.Join(
                        " | ",
                        ModelState.SelectMany(x =>
                            x.Value!.Errors.Select(e =>
                                x.Key + ": " + e.ErrorMessage))));

                return View(model);
            }

            string loginId = model.LoginId?.Trim() ?? "";
            string password = model.Password?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(loginId))
            {
                ModelState.AddModelError(
                    nameof(model.LoginId),
                    "Please enter Login ID.");

                return View(model);
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(
                    nameof(model.Password),
                    "Please enter Password.");

                return View(model);
            }

            if (!workingDate.HasValue)
            {
                ModelState.AddModelError(
                    "",
                    "Please select Working Date.");

                return View(model);
            }

            string organisationCode =
                HttpContext.Session.GetString("OrganisationCode")!;

            string organisationDatabase =
                HttpContext.Session.GetString("OrganisationDatabase") ?? "";

            DbConnection connection =
                _context.Database.GetDbConnection();

            bool shouldClose =
                connection.State != ConnectionState.Open;

            try
            {
                if (shouldClose)
                {
                    await connection.OpenAsync();
                }

                string dbLoginId;
                string userName;
                string marathiName;
                string authority;
                string access;

                // Login ID = UserMast.ini
                // Password = UserMast.code
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT TOP 1
    code,
    ini,
    name,
    mname,
    authority,
    access
FROM dbo.UserMast
WHERE LTRIM(RTRIM(ini)) = @LoginId
AND CONVERT(NVARCHAR(50), code) = @Password;";

                    AddParameter(command, "@LoginId", loginId);
                    AddParameter(command, "@Password", password);

                    using DbDataReader reader =
                        await command.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        ViewBag.LoginError = true;
                        return View(model);
                    }

                    dbLoginId =
                        Convert.ToString(reader["ini"])?.Trim() ?? "";

                    userName =
                        reader["name"] == DBNull.Value
                            ? ""
                            : Convert.ToString(reader["name"])?.Trim() ?? "";

                    marathiName =
                        reader["mname"] == DBNull.Value
                            ? ""
                            : Convert.ToString(reader["mname"])?.Trim() ?? "";

                    authority =
                        reader["authority"] == DBNull.Value
                            ? ""
                            : Convert.ToString(reader["authority"])?.Trim() ?? "";

                    access =
                        reader["access"] == DBNull.Value
                            ? ""
                            : Convert.ToString(reader["access"])?.Trim() ?? "";
                }

                string dbOrganisationLoginKey =
                    GetOrganisationLoginKey(dbLoginId);

                var previous =
                    _activeSessions.Get(dbOrganisationLoginKey);

                // Another active login exists.
                // Credentials have already been verified.
                if (previous != null)
                {
                    CreateTakeoverChallenge(
                        dbLoginId,
                        previous.SessionToken,
                        workingDate.Value);

                    ShowExistingSession(
                        previous,
                        model,
                        workingDate.Value);

                    return View(model);
                }

                // No previous session: continue normal login.
                return await CompleteLogin(
                    dbLoginId,
                    userName,
                    marathiName,
                    authority,
                    access,
                    workingDate.Value,
                    null,
                    connection);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed.");

                ModelState.AddModelError(
                    "",
                    "Unable to login. Please check Visual Studio Output for details.");

                return View(model);
            }
            finally
            {
                if (shouldClose &&
                    connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }

        // Create a short-lived, one-time takeover challenge.
        private void CreateTakeoverChallenge(
            string loginId,
            string expectedToken,
            DateTime workingDate)
        {
            ClearTakeoverChallenge();

            string challenge = Guid.NewGuid().ToString("N");

            HttpContext.Session.SetString(
                TakeoverChallengeKey,
                challenge);

            HttpContext.Session.SetString(
                TakeoverLoginIdKey,
                loginId);

            HttpContext.Session.SetString(
                TakeoverExpectedTokenKey,
                expectedToken);

            HttpContext.Session.SetString(
                TakeoverWorkingDateKey,
                workingDate.ToString("yyyy-MM-dd"));

            HttpContext.Session.SetString(
                TakeoverExpiryKey,
                DateTime.UtcNow.AddMinutes(5).ToString("O"));
        }

        // POST: /Account/ConfirmTakeover
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmTakeover(
            [FromForm] string? challenge)
        {
            if (!IsOrganisationSelected())
            {
                return RedirectToAction("Index", "Organisation");
            }

            string? storedChallenge =
                HttpContext.Session.GetString(TakeoverChallengeKey);

            string? loginId =
                HttpContext.Session.GetString(TakeoverLoginIdKey);

            string? expectedToken =
                HttpContext.Session.GetString(TakeoverExpectedTokenKey);

            string? workingDateText =
                HttpContext.Session.GetString(TakeoverWorkingDateKey);

            string? expiryText =
                HttpContext.Session.GetString(TakeoverExpiryKey);

            // Consume challenge before further processing.
            ClearTakeoverChallenge();

            if (string.IsNullOrWhiteSpace(challenge) ||
                string.IsNullOrWhiteSpace(storedChallenge) ||
                !string.Equals(
                    challenge,
                    storedChallenge,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(loginId) ||
                string.IsNullOrWhiteSpace(expectedToken) ||
                !DateTime.TryParse(
                    expiryText,
                    null,
                    DateTimeStyles.RoundtripKind,
                    out DateTime expiryUtc) ||
                DateTime.UtcNow > expiryUtc)
            {
                TempData["LoginMessage"] =
                    "Confirmation expired. Please login again.";

                return RedirectToAction("Login");
            }

            if (!DateTime.TryParseExact(
                workingDateText,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime workingDate))
            {
                TempData["LoginMessage"] =
                    "Working Date is invalid. Please login again.";

                return RedirectToAction("Login");
            }

            string sessionKey =
                GetOrganisationLoginKey(loginId);

            var current =
                _activeSessions.Get(sessionKey);

            // Confirm that the previous session is still the same.
            if (current == null ||
                !string.Equals(
                    current.SessionToken,
                    expectedToken,
                    StringComparison.Ordinal))
            {
                TempData["LoginMessage"] =
                    "Active session has changed. Please login again.";

                return RedirectToAction("Login");
            }

            DbConnection connection =
                _context.Database.GetDbConnection();

            bool shouldClose =
                connection.State != ConnectionState.Open;

            try
            {
                if (shouldClose)
                {
                    await connection.OpenAsync();
                }

                string userName;
                string marathiName;
                string authority;
                string access;
                string dbLoginId;

                // Re-read user details.
                // Password is not stored in Session.
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT TOP 1
    ini,
    name,
    mname,
    authority,
    access
FROM dbo.UserMast
WHERE LTRIM(RTRIM(ini)) = @LoginId;";

                    AddParameter(command, "@LoginId", loginId);

                    using DbDataReader reader =
                        await command.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        TempData["LoginMessage"] =
                            "User not found. Please login again.";

                        return RedirectToAction("Login");
                    }

                    dbLoginId =
                        Convert.ToString(reader["ini"])?.Trim() ?? "";

                    userName =
                        reader["name"] == DBNull.Value
                            ? ""
                            : Convert.ToString(reader["name"])?.Trim() ?? "";

                    marathiName =
                        reader["mname"] == DBNull.Value
                            ? ""
                            : Convert.ToString(reader["mname"])?.Trim() ?? "";

                    authority =
                        reader["authority"] == DBNull.Value
                            ? ""
                            : Convert.ToString(reader["authority"])?.Trim() ?? "";

                    access =
                        reader["access"] == DBNull.Value
                            ? ""
                            : Convert.ToString(reader["access"])?.Trim() ?? "";
                }

                if (string.IsNullOrWhiteSpace(dbLoginId))
                {
                    TempData["LoginMessage"] =
                        "Invalid Login ID. Please login again.";

                    return RedirectToAction("Login");
                }

                string organisationLoginKey =
                    GetOrganisationLoginKey(dbLoginId);

                if (!string.Equals(
                    organisationLoginKey,
                    sessionKey,
                    StringComparison.OrdinalIgnoreCase))
                {
                    TempData["LoginMessage"] =
                        "Organisation or Login ID changed. Please login again.";

                    return RedirectToAction("Login");
                }

                return await CompleteLogin(
                    dbLoginId,
                    userName,
                    marathiName,
                    authority,
                    access,
                    workingDate,
                    expectedToken,
                    connection);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session takeover failed.");

                TempData["LoginMessage"] =
                    "Unable to continue. Please login again.";

                return RedirectToAction("Login");
            }
            finally
            {
                if (shouldClose &&
                    connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }

        // Common login completion method.
        private async Task<IActionResult> CompleteLogin(
            string dbLoginId,
            string userName,
            string marathiName,
            string authority,
            string access,
            DateTime workingDate,
            string? expectedOldToken,
            DbConnection connection)
        {
            string organisationCode =
                HttpContext.Session.GetString("OrganisationCode") ?? "";

            string organisationDatabase =
                HttpContext.Session.GetString("OrganisationDatabase") ?? "";

            string? selectedOrganisationName =
                HttpContext.Session.GetString("OrganisationName");

            string organisationLoginKey =
                GetOrganisationLoginKey(dbLoginId);

            var previous =
                _activeSessions.Get(organisationLoginKey);

            // Do not replace a session that changed after confirmation.
            if (previous != null &&
                !string.Equals(
                    previous.SessionToken,
                    expectedOldToken,
                    StringComparison.Ordinal))
            {
                TempData["LoginMessage"] =
                    "Another active session exists. Please try again.";

                return RedirectToAction("Login");
            }

            var next = new ActiveLoginSession
            {
                LoginId = organisationLoginKey,
                SessionToken = Guid.NewGuid().ToString("N"),
                BrowserName = DetectBrowser(
                    Request.Headers.UserAgent.ToString()),
                DeviceName = DetectDevice(
                    Request.Headers.UserAgent.ToString()),
                IpAddress = FormatIp(
                    HttpContext.Connection.RemoteIpAddress),
                LoginTimeUtc = DateTime.UtcNow
            };

            // Update Working Date.
            using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
UPDATE dbo.[SYSTEM]
SET [date] = @WorkingDate;";

                AddParameter(
                    command,
                    "@WorkingDate",
                    workingDate.Date);

                int rowsAffected =
                    await command.ExecuteNonQueryAsync();

                if (rowsAffected <= 0)
                {
                    TempData["LoginMessage"] =
                        "Working Date could not be updated in dbo.SYSTEM.";

                    return RedirectToAction("Login");
                }
            }

            if (!_activeSessions.TryStart(
                next,
                expectedOldToken,
                out var conflict))
            {
                TempData["LoginMessage"] =
                    conflict != null
                        ? "Another active session exists. Please try again."
                        : "Session changed. Please try again.";

                return RedirectToAction("Login");
            }

            // Preserve Organisation information.
            HttpContext.Session.Clear();

            HttpContext.Session.SetString(
                "OrganisationCode",
                organisationCode);

            HttpContext.Session.SetString(
                "OrganisationDatabase",
                organisationDatabase);

            if (!string.IsNullOrWhiteSpace(selectedOrganisationName))
            {
                HttpContext.Session.SetString(
                    "OrganisationName",
                    selectedOrganisationName);
            }

            HttpContext.Session.SetString(
                "LoginId",
                dbLoginId);

            HttpContext.Session.SetString(
                "UserName",
                userName);

            HttpContext.Session.SetString(
                "MarathiName",
                marathiName);

            HttpContext.Session.SetString(
                "Authority",
                authority);

            HttpContext.Session.SetString(
                "Access",
                access);

            HttpContext.Session.SetString(
                "WorkingDate",
                workingDate.ToString("yyyy-MM-dd"));

            HttpContext.Session.SetString(
                "ActiveSessionToken",
                next.SessionToken);

            _logger.LogInformation(
                "Login accepted for Organisation {OrganisationCode}, account {LoginId}.",
                organisationCode,
                dbLoginId);

            return RedirectToAction("Index", "Home");
        }

        private void ClearTakeoverChallenge()
        {
            HttpContext.Session.Remove(TakeoverChallengeKey);
            HttpContext.Session.Remove(TakeoverLoginIdKey);
            HttpContext.Session.Remove(TakeoverExpectedTokenKey);
            HttpContext.Session.Remove(TakeoverWorkingDateKey);
            HttpContext.Session.Remove(TakeoverExpiryKey);
        }

        private void ClearLoginSession()
        {
            HttpContext.Session.Remove("LoginId");
            HttpContext.Session.Remove("UserName");
            HttpContext.Session.Remove("MarathiName");
            HttpContext.Session.Remove("Authority");
            HttpContext.Session.Remove("Access");
            HttpContext.Session.Remove("WorkingDate");
            HttpContext.Session.Remove("ActiveSessionToken");
        }

        private void ShowExistingSession(
            ActiveLoginSession previous,
            LoginViewModel model,
            DateTime workingDate)
        {
            ViewBag.ExistingSession = previous;
            ViewBag.WorkingDate = workingDate.ToString("yyyy-MM-dd");

            ViewBag.TakeoverChallenge =
                HttpContext.Session.GetString(TakeoverChallengeKey);

            ViewBag.ExpectedSessionToken =
                previous.SessionToken;
        }

        // POST: /Account/ClaimTab
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClaimTab([FromForm] string? tabId)
        {
            if (string.IsNullOrWhiteSpace(tabId) || tabId.Length > 100)
            {
                return BadRequest(new
                {
                    active = false,
                    message = "Invalid tab ID."
                });
            }

            string? loginId =
                HttpContext.Session.GetString("LoginId");

            string? token =
                HttpContext.Session.GetString("ActiveSessionToken");

            if (string.IsNullOrWhiteSpace(loginId) ||
                string.IsNullOrWhiteSpace(token))
            {
                return Unauthorized(new { active = false });
            }

            string sessionKey =
                GetOrganisationLoginKey(loginId);

            if (!_activeSessions.IsCurrent(sessionKey, token))
            {
                return Unauthorized(new { active = false });
            }

            bool active = _activeSessions.TryClaimTab(
                sessionKey,
                token,
                tabId);

            return Json(new { active });
        }

        // POST: /Account/CheckTab
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CheckTab(
            [FromForm] string? tabId,
            [FromForm] string? page)
        {
            if (string.IsNullOrWhiteSpace(tabId) || tabId.Length > 100)
            {
                return BadRequest(new { active = false });
            }

            string? loginId =
                HttpContext.Session.GetString("LoginId");

            string? token =
                HttpContext.Session.GetString("ActiveSessionToken");

            if (string.IsNullOrWhiteSpace(loginId) ||
                string.IsNullOrWhiteSpace(token))
            {
                return Unauthorized(new { active = false });
            }

            string sessionKey =
                GetOrganisationLoginKey(loginId);

            if (!_activeSessions.IsCurrent(sessionKey, token))
            {
                return Unauthorized(new { active = false });
            }

            bool active = _activeSessions.CheckTab(
                sessionKey,
                token,
                tabId,
                page);

            bool unclaimed =
                _activeSessions.GetActiveTab(sessionKey, token) == null;

            return Json(new { active, unclaimed });
        }

        // POST: /Account/TransferTab
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TransferTab(
            [FromForm] string? tabId,
            [FromForm] string? expectedOldTabId)
        {
            if (string.IsNullOrWhiteSpace(tabId) ||
                string.IsNullOrWhiteSpace(expectedOldTabId) ||
                tabId.Length > 100 ||
                expectedOldTabId.Length > 100 ||
                string.Equals(
                    tabId,
                    expectedOldTabId,
                    StringComparison.Ordinal))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid tab information."
                });
            }

            string? loginId =
                HttpContext.Session.GetString("LoginId");

            string? token =
                HttpContext.Session.GetString("ActiveSessionToken");

            if (string.IsNullOrWhiteSpace(loginId) ||
                string.IsNullOrWhiteSpace(token))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Login session is no longer valid."
                });
            }

            string sessionKey =
                GetOrganisationLoginKey(loginId);

            if (!_activeSessions.IsCurrent(sessionKey, token))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Login session is no longer valid."
                });
            }

            bool success = _activeSessions.TransferTab(
                sessionKey,
                token,
                expectedOldTabId,
                tabId);

            if (!success)
            {
                return Conflict(new
                {
                    success = false,
                    message =
                        "Active tab has changed. Refresh the login page and try again."
                });
            }

            string redirectUrl =
                Url.Action("Index", "Home") ?? "/Home/Index";

            return Json(new
            {
                success = true,
                redirectUrl
            });
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            string? loginId =
                HttpContext.Session.GetString("LoginId");

            string? token =
                HttpContext.Session.GetString("ActiveSessionToken");

            if (!string.IsNullOrWhiteSpace(loginId) &&
                !string.IsNullOrWhiteSpace(token))
            {
                string sessionKey =
                    GetOrganisationLoginKey(loginId);

                _activeSessions.RemoveIfCurrent(
                    sessionKey,
                    token);
            }

            HttpContext.Session.Clear();

            return RedirectToAction("Index", "Organisation");
        }

        private static string FormatIp(System.Net.IPAddress? ip)
        {
            if (ip == null)
                return "Unavailable";

            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();

            if (System.Net.IPAddress.IsLoopback(ip))
                return "127.0.0.1 (this computer)";

            return ip.ToString();
        }

        private static string DetectBrowser(string ua)
        {
            if (ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase))
                return "Microsoft Edge";

            if (ua.Contains("OPR/", StringComparison.OrdinalIgnoreCase))
                return "Opera";

            if (ua.Contains("Firefox/", StringComparison.OrdinalIgnoreCase))
                return "Mozilla Firefox";

            if (ua.Contains("Chrome/", StringComparison.OrdinalIgnoreCase))
                return "Google Chrome";

            if (ua.Contains("Safari/", StringComparison.OrdinalIgnoreCase))
                return "Safari";

            return "Unknown browser";
        }

        private static string DetectDevice(string ua)
        {
            if (ua.Contains("Android", StringComparison.OrdinalIgnoreCase))
                return "Android Device";

            if (ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
                return "iPhone";

            if (ua.Contains("iPad", StringComparison.OrdinalIgnoreCase))
                return "iPad";

            if (ua.Contains("Windows", StringComparison.OrdinalIgnoreCase))
                return "Windows PC";

            if (ua.Contains("Macintosh", StringComparison.OrdinalIgnoreCase))
                return "Mac";

            if (ua.Contains("Linux", StringComparison.OrdinalIgnoreCase))
                return "Linux Device";

            return "Unknown device";
        }

        private static void AddParameter(
            DbCommand command,
            string name,
            object? value)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}
