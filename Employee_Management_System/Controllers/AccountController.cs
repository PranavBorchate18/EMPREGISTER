using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

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
            string? loginId =
                HttpContext.Session.GetString("LoginId");

            if (!string.IsNullOrWhiteSpace(loginId))
            {
                return RedirectToAction(
                    "Register",
                    "Employee"
                );
            }

            return View(new LoginViewModel());
        }


        // =====================================================
        // LOGIN POST
        //
        // Login ID = dbo.UserMast.ini
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }


            string loginId =
                model.LoginId?.Trim() ?? "";


            if (string.IsNullOrWhiteSpace(loginId))
            {
                ModelState.AddModelError(
                    nameof(model.LoginId),
                    "Please enter Login ID."
                );

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


                using DbCommand command =
                    connection.CreateCommand();


                command.CommandText = @"
SELECT TOP 1
       ini,
       name,
       mname,
       authority,
       access
FROM dbo.UserMast
WHERE LTRIM(RTRIM(ini)) = @LoginId;
";


                AddParameter(
                    command,
                    "@LoginId",
                    loginId
                );


                using DbDataReader reader =
                    await command.ExecuteReaderAsync();


                // =================================================
                // INVALID LOGIN
                // =================================================
                if (!await reader.ReadAsync())
                {
                    ModelState.AddModelError(
                        nameof(model.LoginId),
                        "Invalid Login ID."
                    );

                    return View(model);
                }


                // =================================================
                // READ USER INFORMATION
                // =================================================

                string dbLoginId =
                    reader["ini"] == DBNull.Value
                        ? ""
                        : Convert.ToString(
                            reader["ini"]
                        )?.Trim() ?? "";


                string userName =
                    reader["name"] == DBNull.Value
                        ? ""
                        : Convert.ToString(
                            reader["name"]
                        )?.Trim() ?? "";


                string marathiName =
                    reader["mname"] == DBNull.Value
                        ? ""
                        : Convert.ToString(
                            reader["mname"]
                        )?.Trim() ?? "";


                string authority =
                    reader["authority"] == DBNull.Value
                        ? ""
                        : Convert.ToString(
                            reader["authority"]
                        )?.Trim() ?? "";


                string access =
                    reader["access"] == DBNull.Value
                        ? ""
                        : Convert.ToString(
                            reader["access"]
                        )?.Trim() ?? "";


                // =================================================
                // CREATE SESSION
                //
                // MOST IMPORTANT:
                //
                // UserMast.ini
                //       ↓
                // Session["LoginId"]
                //
                // =================================================

                HttpContext.Session.SetString(
                    "LoginId",
                    dbLoginId
                );


                HttpContext.Session.SetString(
                    "UserName",
                    userName
                );


                HttpContext.Session.SetString(
                    "MarathiName",
                    marathiName
                );


                HttpContext.Session.SetString(
                    "Authority",
                    authority
                );


                HttpContext.Session.SetString(
                    "Access",
                    access
                );


                // =================================================
                // LOGIN SUCCESS
                // =================================================

                return RedirectToAction(
                    "Register",
                    "Employee"
                );
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(
                    "",
                    "Unable to login. " +
                    ex.Message
                );

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

            return RedirectToAction(
                "Login",
                "Account"
            );
        }


        // =====================================================
        // COMMON PARAMETER METHOD
        // =====================================================
        private static void AddParameter(
            DbCommand command,
            string name,
            object? value)
        {
            DbParameter parameter =
                command.CreateParameter();

            parameter.ParameterName = name;

            parameter.Value =
                value ?? DBNull.Value;

            command.Parameters.Add(parameter);
        }
    }
}