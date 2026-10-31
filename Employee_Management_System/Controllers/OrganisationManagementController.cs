using Employee_Management_System.Data;
using Employee_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Employee_Management_System.Controllers
{
    /// <summary>
    /// Create new organisations (and their databases).
    ///
    /// Access rule (checked on every request, server side):
    ///   - the user must be logged in (Program.cs middleware already enforces this), AND
    ///   - must be logged in to the master organisation (OrganisationProvisioning:MasterOrganisationCode), AND
    ///   - UserMast.authority must be in OrganisationProvisioning:AllowedAuthorities.
    /// </summary>
    public class OrganisationManagementController : Controller
    {
        private readonly OrganisationDbContext _organisations;
        private readonly OrganisationProvisioningService _provisioning;
        private readonly ILogger<OrganisationManagementController> _logger;

        public OrganisationManagementController(
            OrganisationDbContext organisations,
            OrganisationProvisioningService provisioning,
            ILogger<OrganisationManagementController> logger)
        {
            _organisations = organisations;
            _provisioning = provisioning;
            _logger = logger;
        }

        private bool IsAuthorised()
        {
            int? organisationCode = HttpContext.Session.GetInt32("OrganisationCode");
            string authority =
                (HttpContext.Session.GetString("Authority") ?? "").Trim();

            if (!organisationCode.HasValue ||
                organisationCode.Value != _provisioning.Options.MasterOrganisationCode)
            {
                return false;
            }

            return _provisioning.Options.AllowedAuthorities.Any(a =>
                string.Equals(a?.Trim(), authority, StringComparison.OrdinalIgnoreCase));
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsAuthorised())
                return View("AccessDenied");

            await LoadListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int code, string? organisationName)
        {
            if (!IsAuthorised())
            {
                _logger.LogWarning(
                    "Unauthorised organisation creation attempt by login {LoginId}.",
                    HttpContext.Session.GetString("LoginId"));

                return View("AccessDenied");
            }

            var result = await _provisioning.CreateOrganisationAsync(
                code,
                organisationName,
                HttpContext.RequestAborted);

            ViewBag.Result = result;
            ViewBag.EnteredCode = code;
            ViewBag.EnteredName = organisationName;

            await LoadListAsync();
            return View("Index");
        }

        private async Task LoadListAsync()
        {
            ViewBag.Organisations = await _organisations.OrganisationMasters
                .AsNoTracking()
                .OrderBy(x => x.Code)
                .ToListAsync();
        }
    }
}