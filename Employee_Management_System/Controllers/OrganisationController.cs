
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

            if (HttpContext.Session.GetInt32("OrganisationCode").HasValue &&
                !string.IsNullOrWhiteSpace(
                    HttpContext.Session.GetString("OrganisationName")))
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

                    ViewBag.Error = "Invalid Organisation Code.";
                    return View();
                }

                // STEP 2: Select Database
                string databaseName = code == 1
                    ? "PayData_STM"
                    : $"PayData_{code}";

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

                var connectionBuilder =
                    new SqlConnectionStringBuilder(baseConnectionString)
                    {
                        InitialCatalog = databaseName
                    };

                // STEP 4: Verify Database Connection
                await using (var connection =
                    new SqlConnection(connectionBuilder.ConnectionString))
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

                _logger.LogInformation(
                    "Organisation selected successfully. Code: {Code}, Name: {Name}",
                    organisation.Code,
                    organisation.OrganisationName);

                // STEP 8: Redirect to Login
                return RedirectToAction("Login", "Account");
            }
            catch (SqlException ex)
            {
                _logger.LogError(
                    ex,
                    "SQL error while selecting Organisation Code: {Code}",
                    code);

                ViewBag.Error =
                    "Database connection failed. Please check the selected Organisation Database.";

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while selecting Organisation Code: {Code}",
                    code);

                ViewBag.Error =
                    "Unable to continue. Error: " + ex.Message;

                return View();
            }
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
