using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Employee_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace Employee_Management_System.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ActiveSessionService _activeSessions;
        private readonly Microsoft.Extensions.Logging.ILogger<AccountController> _logger;

        public AccountController(ApplicationDbContext context, ActiveSessionService activeSessions,
            Microsoft.Extensions.Logging.ILogger<AccountController> logger)
        {
            _context = context;
            _activeSessions = activeSessions;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login()
        {
            string? loginId = HttpContext.Session.GetString("LoginId");
            string? token = HttpContext.Session.GetString("ActiveSessionToken");
            if (!string.IsNullOrWhiteSpace(loginId) && !string.IsNullOrWhiteSpace(token)
                && _activeSessions.IsCurrent(loginId, token))
                return RedirectToAction("Register", "Employee");

            HttpContext.Session.Clear();
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, DateTime? workingDate,
            bool forceLogin = false, string? expectedSessionToken = null)
        {
            ViewBag.WorkingDate = workingDate?.ToString("yyyy-MM-dd");
            if (!ModelState.IsValid)
            {
                // Show validation errors instead of silently redisplaying Login.
                _logger.LogWarning("Login validation failed: {Errors}", string.Join(" | ",
                    ModelState.SelectMany(x => x.Value!.Errors.Select(e => x.Key + ": " + e.ErrorMessage))));
                ModelState.AddModelError("", "Login form validation failed. Please check the messages below.");
                return View(model);
            }

            string loginId = model.LoginId?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(loginId))
            {
                ModelState.AddModelError(nameof(model.LoginId), "Please enter Login ID.");
                return View(model);
            }
            if (!workingDate.HasValue)
            {
                ModelState.AddModelError("", "Please select Working Date.");
                return View(model);
            }

            DbConnection connection = _context.Database.GetDbConnection();
            bool shouldClose = connection.State != ConnectionState.Open;
            try
            {
                if (shouldClose) await connection.OpenAsync();

                string dbLoginId, userName, marathiName, authority, access;
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"SELECT TOP 1 ini, name, mname, authority, access
FROM dbo.UserMast WHERE LTRIM(RTRIM(ini)) = @LoginId;";
                    AddParameter(command, "@LoginId", loginId);
                    using DbDataReader reader = await command.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        ModelState.AddModelError(nameof(model.LoginId), "Invalid Login ID.");
                        return View(model);
                    }
                    dbLoginId = Convert.ToString(reader["ini"])?.Trim() ?? "";
                    userName = reader["name"] == DBNull.Value ? "" : Convert.ToString(reader["name"])?.Trim() ?? "";
                    marathiName = reader["mname"] == DBNull.Value ? "" : Convert.ToString(reader["mname"])?.Trim() ?? "";
                    authority = reader["authority"] == DBNull.Value ? "" : Convert.ToString(reader["authority"])?.Trim() ?? "";
                    access = reader["access"] == DBNull.Value ? "" : Convert.ToString(reader["access"])?.Trim() ?? "";
                }

                // Existing login logic validates only Login ID, NOT password.
                // Do not expose this application on untrusted networks until proper authentication is added.
                var previous = _activeSessions.Get(dbLoginId);
                if (previous != null &&
                    (!forceLogin || previous.SessionToken != expectedSessionToken))
                {
                    ShowExistingSession(previous, model, workingDate.Value);
                    return View(model);
                }

                var next = new ActiveLoginSession
                {
                    LoginId = dbLoginId,
                    SessionToken = Guid.NewGuid().ToString("N"),
                    BrowserName = DetectBrowser(Request.Headers.UserAgent.ToString()),
                    DeviceName = DetectDevice(Request.Headers.UserAgent.ToString()),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unavailable",
                    LoginTimeUtc = DateTime.UtcNow
                };

                // Update working date BEFORE replacing an existing active session.
                // A database error must not invalidate the previous user's session.
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"UPDATE dbo.[SYSTEM] SET [date] = @WorkingDate;";
                    AddParameter(command, "@WorkingDate", workingDate.Value.Date);
                    if (await command.ExecuteNonQueryAsync() <= 0)
                    {
                        ModelState.AddModelError("", "Working Date could not be updated in dbo.SYSTEM.");
                        return View(model);
                    }
                }

                // Atomic replacement: reject a stale popup confirmation.
                if (!_activeSessions.TryStart(next, previous?.SessionToken, out var conflict))
                {
                    if (conflict != null) ShowExistingSession(conflict, model, workingDate.Value);
                    else ModelState.AddModelError("", "Session changed. Please try again.");
                    return View(model);
                }

                HttpContext.Session.Clear();
                HttpContext.Session.SetString("LoginId", dbLoginId);
                HttpContext.Session.SetString("UserName", userName);
                HttpContext.Session.SetString("MarathiName", marathiName);
                HttpContext.Session.SetString("Authority", authority);
                HttpContext.Session.SetString("Access", access);
                HttpContext.Session.SetString("WorkingDate", workingDate.Value.ToString("yyyy-MM-dd"));
                HttpContext.Session.SetString("ActiveSessionToken", next.SessionToken);
                _logger.LogInformation("Login accepted for account {LoginId}; redirecting to Employee/Register.", dbLoginId);
                return RedirectToAction("Register", "Employee");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed.");
                ModelState.AddModelError("", "Unable to login. Please check Visual Studio Output for details.");
                return View(model);
            }
            finally
            {
                if (shouldClose && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }

        private void ShowExistingSession(ActiveLoginSession previous, LoginViewModel model, DateTime workingDate)
        {
            ViewBag.ExistingSession = previous;
            ViewBag.WorkingDate = workingDate.ToString("yyyy-MM-dd");
            // Token is compared server-side to prevent stale confirmations from replacing newer logins.
            ViewBag.ExpectedSessionToken = previous.SessionToken;
        }

        // Single Active Tab: claim the first tab for the current login.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClaimTab([FromForm] string tabId)
        {
            var loginId = HttpContext.Session.GetString("LoginId");
            var token = HttpContext.Session.GetString("ActiveSessionToken");
            if (string.IsNullOrWhiteSpace(loginId) || string.IsNullOrWhiteSpace(token))
                return Unauthorized(new { active = false });

            bool active = _activeSessions.TryClaimTab(loginId, token, tabId);
            return Json(new { active });
        }

        // Single Active Tab: subsequent checks must match the claimed tab.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CheckTab([FromForm] string tabId)
        {
            var loginId = HttpContext.Session.GetString("LoginId");
            var token = HttpContext.Session.GetString("ActiveSessionToken");
            if (string.IsNullOrWhiteSpace(loginId) || string.IsNullOrWhiteSpace(token))
                return Unauthorized(new { active = false });

            bool active = _activeSessions.CheckTab(loginId, token, tabId);
            return Json(new { active, unclaimed = false });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            string? loginId = HttpContext.Session.GetString("LoginId");
            string? token = HttpContext.Session.GetString("ActiveSessionToken");
            if (!string.IsNullOrWhiteSpace(loginId) && !string.IsNullOrWhiteSpace(token))
                _activeSessions.RemoveIfCurrent(loginId, token);
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        private static string DetectBrowser(string ua)
        {
            if (ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase)) return "Microsoft Edge";
            if (ua.Contains("OPR/", StringComparison.OrdinalIgnoreCase)) return "Opera";
            if (ua.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)) return "Mozilla Firefox";
            if (ua.Contains("Chrome/", StringComparison.OrdinalIgnoreCase)) return "Google Chrome";
            if (ua.Contains("Safari/", StringComparison.OrdinalIgnoreCase)) return "Safari";
            return "Unknown browser";
        }

        private static string DetectDevice(string ua)
        {
            if (ua.Contains("Android", StringComparison.OrdinalIgnoreCase)) return "Android Device";
            if (ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase)) return "iPhone";
            if (ua.Contains("iPad", StringComparison.OrdinalIgnoreCase)) return "iPad";
            if (ua.Contains("Windows", StringComparison.OrdinalIgnoreCase)) return "Windows PC";
            if (ua.Contains("Macintosh", StringComparison.OrdinalIgnoreCase)) return "Mac";
            if (ua.Contains("Linux", StringComparison.OrdinalIgnoreCase)) return "Linux Device";
            return "Unknown device";
        }

        private static void AddParameter(DbCommand command, string name, object? value)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}
