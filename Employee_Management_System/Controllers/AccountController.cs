using Employee_Management_System.Data;
using Employee_Management_System.Models;
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

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // LOGIN PAGE
        // =====================================================
        [HttpGet]
        public IActionResult Login()
        {
            string? loginId = HttpContext.Session.GetString("LoginId");

            if (!string.IsNullOrWhiteSpace(loginId))
            {
                return RedirectToAction("Register", "Employee");
            }

            return View(new LoginViewModel());
        }

        // =====================================================
        // LOGIN POST
        //
        // Login ID     = dbo.UserMast.ini
        // Working Date = dbo.SYSTEM.date
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model,
            DateTime? workingDate)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.WorkingDate = workingDate?.ToString("yyyy-MM-dd");
                return View(model);
            }

            string loginId = model.LoginId?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(loginId))
            {
                ModelState.AddModelError(
                    nameof(model.LoginId),
                    "Please enter Login ID.");

                ViewBag.WorkingDate = workingDate?.ToString("yyyy-MM-dd");
                return View(model);
            }

            if (!workingDate.HasValue)
            {
                ModelState.AddModelError(
                    "",
                    "Please select Working Date.");

                return View(model);
            }

            DbConnection connection = _context.Database.GetDbConnection();
            bool shouldClose = connection.State != ConnectionState.Open;

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

                // =================================================
                // VALIDATE LOGIN
                // =================================================
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
                        ModelState.AddModelError(
                            nameof(model.LoginId),
                            "Invalid Login ID.");

                        ViewBag.WorkingDate =
                            workingDate.Value.ToString("yyyy-MM-dd");

                        return View(model);
                    }

                    dbLoginId =
                        reader["ini"] == DBNull.Value
                            ? ""
                            : Convert.ToString(reader["ini"])?.Trim() ?? "";

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

                // =================================================
                // UPDATE WORKING DATE IN dbo.SYSTEM
                // =================================================
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
UPDATE dbo.[SYSTEM]
SET [date] = @WorkingDate;";

                    AddParameter(
                        command,
                        "@WorkingDate",
                        workingDate.Value.Date);

                    int affectedRows =
                        await command.ExecuteNonQueryAsync();

                    if (affectedRows <= 0)
                    {
                        ModelState.AddModelError(
                            "",
                            "Working Date could not be updated in dbo.SYSTEM.");

                        ViewBag.WorkingDate =
                            workingDate.Value.ToString("yyyy-MM-dd");

                        return View(model);
                    }
                }

                // =================================================
                // CREATE SESSION
                // =================================================
                HttpContext.Session.SetString("LoginId", dbLoginId);
                HttpContext.Session.SetString("UserName", userName);
                HttpContext.Session.SetString("MarathiName", marathiName);
                HttpContext.Session.SetString("Authority", authority);
                HttpContext.Session.SetString("Access", access);

                HttpContext.Session.SetString(
                    "WorkingDate",
                    workingDate.Value.ToString("yyyy-MM-dd"));

                // Existing login success flow is preserved.
                return RedirectToAction("Register", "Employee");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(
                    "",
                    "Unable to login. " + ex.Message);

                ViewBag.WorkingDate =
                    workingDate?.ToString("yyyy-MM-dd");

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

        // =====================================================
        // LOGOUT
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            return RedirectToAction("Login", "Account");
        }

        // =====================================================
        // COMMON PARAMETER METHOD
        // =====================================================
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
