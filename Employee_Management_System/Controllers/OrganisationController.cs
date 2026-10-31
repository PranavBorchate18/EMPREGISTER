using Employee_Management_System.Data;
using Employee_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace Employee_Management_System.Controllers
{
    public class OrganisationController : Controller
    {
        private readonly OrganisationDbContext _context;
        private readonly ILogger<OrganisationController> _logger;
        private readonly IConfiguration _configuration;
        private readonly ActiveSessionService _activeSessions;

        // TEMPORARY DIAGNOSTIC (Development only) - see Status() below.
        private static string _lastPost = "No POST received since the app started.";

        public OrganisationController(
            OrganisationDbContext context,
            ILogger<OrganisationController> logger,
            IConfiguration configuration,
            ActiveSessionService activeSessions)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _activeSessions = activeSessions;
        }

        [HttpGet]
        public IActionResult Index()
        {
            _logger.LogInformation(
                "Organisation Selection GET called.");

            if (HttpContext.Session.GetInt32("OrganisationCode").HasValue)
            {
                _logger.LogInformation(
                    "Organisation already selected. Redirecting to Login.");

                return RedirectToAction("Login", "Account");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(int code)
        {
            _logger.LogInformation(
                "Organisation Selection POST called. Code: {Code}",
                code);

            _lastPost = $"{DateTime.Now:HH:mm:ss} POST received, code={code}";

            if (code <= 0)
            {
                ViewBag.Error =
                    "Please enter a valid Organisation Code.";

                return View();
            }

            try
            {
                // STEP 1: OrganisationDB Validation
                _logger.LogInformation(
                    "Checking OrganisationDB for Code: {Code}", code);

                var organisation = await _context.OrganisationMasters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Code == code);

                if (organisation == null)
                {
                    _logger.LogWarning(
                        "Invalid Organisation Code: {Code}", code);

                    _lastPost += " -> code NOT found in OrganisationMaster";
                    ViewBag.Error = "Invalid Organisation Code.";
                    return View();
                }

                // STEP 2: Select Database
                string databaseName =
                    OrganisationDatabaseHelper.GetDatabaseName(code);

                _logger.LogInformation(
                    "Selected Database: {DatabaseName}", databaseName);

                // STEP 3: Get Connection String
                string? baseConnectionString =
                    _configuration.GetConnectionString("DefaultConnection");

                if (string.IsNullOrWhiteSpace(baseConnectionString))
                {
                    throw new InvalidOperationException(
                        "DefaultConnection is not configured in appsettings.json.");
                }

                string organisationConnectionString =
                    OrganisationDatabaseHelper.BuildConnectionString(
                        baseConnectionString,
                        databaseName);

                // STEP 4: Verify Database Connection
                await using (var connection =
                    new SqlConnection(organisationConnectionString))
                {
                    await connection.OpenAsync();

                    _logger.LogInformation(
                        "Database connection successful: {DatabaseName}",
                        databaseName);
                }

                // STEP 5: Remove Previous Active Session
                string? oldLoginId =
                    HttpContext.Session.GetString("LoginId");

                string? oldToken =
                    HttpContext.Session.GetString("ActiveSessionToken");

                int? oldOrganisationCode =
                    HttpContext.Session.GetInt32("OrganisationCode");

                if (!string.IsNullOrWhiteSpace(oldLoginId) &&
                    !string.IsNullOrWhiteSpace(oldToken) &&
                    oldOrganisationCode.HasValue)
                {
                    string oldSessionKey =
                        $"{oldOrganisationCode.Value}:{oldLoginId}";

                    _activeSessions.RemoveIfCurrent(
                        oldSessionKey,
                        oldToken);
                }

                // STEP 6: Clear Old Login Information
                HttpContext.Session.Remove("LoginId");
                HttpContext.Session.Remove("UserName");
                HttpContext.Session.Remove("MarathiName");
                HttpContext.Session.Remove("Authority");
                HttpContext.Session.Remove("Access");
                HttpContext.Session.Remove("WorkingDate");
                HttpContext.Session.Remove("ActiveSessionToken");

                // STEP 7: Save New Organisation Information
                HttpContext.Session.SetInt32(
                    "OrganisationCode",
                    organisation.Code);

                HttpContext.Session.SetString(
                    "OrganisationName",
                    organisation.OrganisationName);

                // Make sure the session is stored before the redirect.
                await HttpContext.Session.CommitAsync();

                _logger.LogInformation(
                    "Organisation selected successfully. Code: {Code}, Name: {Name}. Redirecting to Login.",
                    organisation.Code,
                    organisation.OrganisationName);

                // STEP 8: Redirect to Login
                _lastPost += " -> SUCCESS, session saved, redirecting to /Account/Login";
                return RedirectToAction("Login", "Account");
            }
            catch (SqlException ex)
            {
                _logger.LogError(
                    ex,
                    "SQL error while selecting Organisation Code: {Code}",
                    code);

                _lastPost += $" -> SQL error {ex.Number}: {ex.Message}";

                // 4060 = database does not exist / cannot be opened by this login.
                ViewBag.Error = ex.Number == 4060
                    ? "The database for this organisation does not exist or is not accessible."
                    : "Database connection failed. Please contact the administrator.";

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while selecting Organisation Code: {Code}",
                    code);

                _lastPost += $" -> {ex.GetType().Name}: {ex.Message}";

                // Details stay in the server log; never show them to the browser.
                ViewBag.Error =
                    "Unable to continue. Please contact the administrator.";

                return View();
            }
        }

        // TEMPORARY DIAGNOSTIC: open /Organisation/Status in the same browser after
        // pressing Continue. Works only when ASPNETCORE_ENVIRONMENT = Development.
        // Delete this action once the problem is solved.
        [HttpGet]
        public IActionResult Status()
        {
            var env = HttpContext.RequestServices
                .GetRequiredService<IWebHostEnvironment>();

            if (!env.IsDevelopment())
                return NotFound();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Last POST to Organisation/Index : " + _lastPost);
            sb.AppendLine("Session available               : " + HttpContext.Session.IsAvailable);
            sb.AppendLine("Session Id                      : " + HttpContext.Session.Id);
            sb.AppendLine("Session cookie sent by browser  : " +
                Request.Cookies.ContainsKey(".EmployeeManagement.Session"));
            sb.AppendLine("OrganisationCode in session     : " +
                (HttpContext.Session.GetInt32("OrganisationCode")?.ToString() ?? "(none)"));
            sb.AppendLine("OrganisationName in session     : " +
                (HttpContext.Session.GetString("OrganisationName") ?? "(none)"));

            return Content(sb.ToString(), "text/plain");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangeOrganisation()
        {
            string? loginId =
                HttpContext.Session.GetString("LoginId");

            string? token =
                HttpContext.Session.GetString("ActiveSessionToken");

            int? organisationCode =
                HttpContext.Session.GetInt32("OrganisationCode");

            if (!string.IsNullOrWhiteSpace(loginId) &&
                !string.IsNullOrWhiteSpace(token) &&
                organisationCode.HasValue)
            {
                string sessionKey =
                    $"{organisationCode.Value}:{loginId}";

                _activeSessions.RemoveIfCurrent(
                    sessionKey,
                    token);
            }

            HttpContext.Session.Clear();

            return RedirectToAction("Index");
        }
    }
}