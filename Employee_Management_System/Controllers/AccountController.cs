
using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Employee_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace Employee_Management_System.Controllers
{
    public class AccountController : Controller
    {
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

        // =========================================================
        // LOGIN - GET
        // =========================================================

        [HttpGet]
        public IActionResult Login(bool tabEntry = false, bool tabLost = false)
        {
            string? loginId = HttpContext.Session.GetString("LoginId");
            string? token = HttpContext.Session.GetString("ActiveSessionToken");

            bool valid =
                !string.IsNullOrWhiteSpace(loginId) &&
                !string.IsNullOrWhiteSpace(token) &&
                _activeSessions.IsCurrent(loginId, token);

            // Duplicate tab login page:
            // Do not clear the shared browser session.
            if (tabEntry && valid)
            {
                var tab = _activeSessions.GetActiveTab(loginId!, token!);

                if (tab != null)
                {
                    ViewBag.TabExistingSession = _activeSessions.Get(loginId!);
                    ViewBag.ActiveTabPage = tab.Page;
                    ViewBag.ExpectedOldTabId = tab.TabId;
                }

                ViewBag.TabEntry = true;
                return View(new LoginViewModel());
            }

            if (tabLost)
            {
                ViewBag.TabLost = true;
                return View(new LoginViewModel());
            }

            if (valid)
            {
                return RedirectToAction("Register", "Employee");
            }

            HttpContext.Session.Clear();

            return View(new LoginViewModel());
        }

        // =========================================================
        // LOGIN - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model,
            DateTime? workingDate,
            bool forceLogin = false,
            string? expectedSessionToken = null)
        {
            ViewBag.WorkingDate =
                workingDate?.ToString("yyyy-MM-dd")
                ?? DateTime.Today.ToString("yyyy-MM-dd");

            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "Login validation failed: {Errors}",
                    string.Join(
                        " | ",
                        ModelState
                            .SelectMany(x => x.Value!.Errors)
                            .Select(e => e.ErrorMessage)));

                ModelState.AddModelError(
                    "",
                    "Login form validation failed. Please check the messages below.");

                return View(model);
            }

            string loginId = model.LoginId?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(loginId))
            {
                ModelState.AddModelError(
                    nameof(model.LoginId),
                    "Please enter Login ID.");

                return View(model);
            }

            if (!workingDate.HasValue)
            {
                ModelState.AddModelError(
                    "",
                    "Please select Working Date.");

                return View(model);
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

                string dbLoginId;
                string userName;
                string marathiName;
                string authority;
                string access;

                // -------------------------------------------------
                // Read existing UserMast record
                // -------------------------------------------------

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT TOP 1 ini, name, mname, authority, access
FROM dbo.UserMast
WHERE LTRIM(RTRIM(ini)) = @LoginId;";

                    AddParameter(command, "@LoginId", loginId);

                    using DbDataReader reader =
                        await command.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        ModelState.AddModelError(
                            nameof(model.LoginId),
                            "Invalid Login ID.");

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

                // -------------------------------------------------
                // Check existing active login
                // -------------------------------------------------

                var previous = _activeSessions.Get(dbLoginId);

                if (previous != null)
                {
                    bool confirmedExpectedSession =
                        forceLogin &&
                        !string.IsNullOrWhiteSpace(expectedSessionToken) &&
                        string.Equals(
                            previous.SessionToken,
                            expectedSessionToken,
                            StringComparison.Ordinal);

                    if (!confirmedExpectedSession)
                    {
                        ShowExistingSession(
                            previous,
                            model,
                            workingDate.Value);

                        return View(model);
                    }
                }

                // -------------------------------------------------
                // Prepare new session
                // -------------------------------------------------

                var next = new ActiveLoginSession
                {
                    LoginId = dbLoginId,
                    SessionToken = Guid.NewGuid().ToString("N"),
                    BrowserName = DetectBrowser(
                        Request.Headers.UserAgent.ToString()),
                    DeviceName = DetectDevice(
                        Request.Headers.UserAgent.ToString()),
                    IpAddress =
                        HttpContext.Connection.RemoteIpAddress?.ToString()
                        ?? "Unavailable",
                    LoginTimeUtc = DateTime.UtcNow
                };

                // -------------------------------------------------
                // Update working date before replacing old session
                // -------------------------------------------------

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
UPDATE dbo.[SYSTEM]
SET [date] = @WorkingDate;";

                    AddParameter(
                        command,
                        "@WorkingDate",
                        workingDate.Value.Date);

                    int rowsAffected =
                        await command.ExecuteNonQueryAsync();

                    if (rowsAffected <= 0)
                    {
                        ModelState.AddModelError(
                            "",
                            "Working Date could not be updated in dbo.SYSTEM.");

                        return View(model);
                    }
                }

                // -------------------------------------------------
                // Atomically start or replace active session
                // -------------------------------------------------

                if (!_activeSessions.TryStart(
                    next,
                    previous?.SessionToken,
                    out var conflict))
                {
                    if (conflict != null)
                    {
                        ShowExistingSession(
                            conflict,
                            model,
                            workingDate.Value);
                    }
                    else
                    {
                        ModelState.AddModelError(
                            "",
                            "Session changed. Please try again.");
                    }

                    return View(model);
                }

                // -------------------------------------------------
                // Store session values
                // -------------------------------------------------

                HttpContext.Session.Clear();

                HttpContext.Session.SetString("LoginId", dbLoginId);
                HttpContext.Session.SetString("UserName", userName);
                HttpContext.Session.SetString("MarathiName", marathiName);
                HttpContext.Session.SetString("Authority", authority);
                HttpContext.Session.SetString("Access", access);

                HttpContext.Session.SetString(
                    "WorkingDate",
                    workingDate.Value.ToString("yyyy-MM-dd"));

                HttpContext.Session.SetString(
                    "ActiveSessionToken",
                    next.SessionToken);

                _logger.LogInformation(
                    "Login accepted for account {LoginId}.",
                    dbLoginId);

                return RedirectToAction("Register", "Employee");
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

        // =========================================================
        // DISPLAY EXISTING SESSION POPUP
        // =========================================================

        private void ShowExistingSession(
            ActiveLoginSession previous,
            LoginViewModel model,
            DateTime workingDate)
        {
            ViewBag.ExistingSession = previous;

            ViewBag.WorkingDate =
                workingDate.ToString("yyyy-MM-dd");

            ViewBag.ExpectedSessionToken =
                previous.SessionToken;
        }

        // =========================================================
        // CLAIM ACTIVE TAB
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClaimTab([FromForm] string? tabId)
        {
            if (string.IsNullOrWhiteSpace(tabId) ||
                tabId.Length > 100)
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
                string.IsNullOrWhiteSpace(token) ||
                !_activeSessions.IsCurrent(loginId, token))
            {
                return Unauthorized(new
                {
                    active = false,
                    message = "Session expired. Please login again."
                });
            }

            bool active =
                _activeSessions.TryClaimTab(
                    loginId,
                    token,
                    tabId);

            return Json(new
            {
                active,
                message = active
                    ? "This tab is active."
                    : "Another tab is already active."
            });
        }

        // =========================================================
        // CHECK ACTIVE TAB
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CheckTab(
            [FromForm] string? tabId,
            [FromForm] string? page)
        {
            if (string.IsNullOrWhiteSpace(tabId) ||
                tabId.Length > 100)
            {
                return BadRequest(new
                {
                    active = false,
                    unclaimed = false,
                    message = "Invalid tab ID."
                });
            }

            string? loginId =
                HttpContext.Session.GetString("LoginId");

            string? token =
                HttpContext.Session.GetString("ActiveSessionToken");

            if (string.IsNullOrWhiteSpace(loginId) ||
                string.IsNullOrWhiteSpace(token) ||
                !_activeSessions.IsCurrent(loginId, token))
            {
                return Unauthorized(new
                {
                    active = false,
                    unclaimed = false,
                    message = "Session expired."
                });
            }

            bool active = _activeSessions.CheckTab(
                loginId,
                token,
                tabId,
                page);

            bool unclaimed =
                _activeSessions.GetActiveTab(loginId, token) == null;

            return Json(new
            {
                active,
                unclaimed,
                message = active
                    ? "This tab is active."
                    : "This tab is not the active tab."
            });
        }

        // =========================================================
        // TRANSFER ACTIVE TAB
        // Used by the "Yes, Continue" button on tab-entry page.
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TransferTab(
            [FromForm] string? tabId,
            [FromForm] string? expectedOldTabId)
        {
            if (string.IsNullOrWhiteSpace(tabId) ||
                tabId.Length > 100 ||
                string.IsNullOrWhiteSpace(expectedOldTabId) ||
                expectedOldTabId.Length > 100)
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
                string.IsNullOrWhiteSpace(token) ||
                !_activeSessions.IsCurrent(loginId, token))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Session expired. Please login again."
                });
            }

            bool success = _activeSessions.TransferTab(
                loginId,
                token,
                expectedOldTabId,
                tabId);

            if (!success)
            {
                return Conflict(new
                {
                    success = false,
                    message =
                        "The active tab has changed. Refresh the login page and try again."
                });
            }

            return Json(new
            {
                success = true,
                redirectUrl = Url.Action("Register", "Employee")
            });
        }

        // =========================================================
        // LOGOUT
        // =========================================================

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
                _activeSessions.RemoveIfCurrent(loginId, token);
            }

            HttpContext.Session.Clear();

            return RedirectToAction("Login", "Account");
        }

        // =========================================================
        // BROWSER DETECTION
        // =========================================================

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

        // =========================================================
        // DEVICE DETECTION
        // =========================================================

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

        // =========================================================
        // DATABASE PARAMETER
        // =========================================================

        private static void AddParameter(
            DbCommand command,
            string name,
            object? value)
        {
            DbParameter parameter =
                command.CreateParameter();

            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;

            command.Parameters.Add(parameter);
        }
    }
}
