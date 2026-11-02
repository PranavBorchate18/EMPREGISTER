using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Employee_Management_System.Controllers
{
    public class EmployeeTypeController : Controller
    {
        private readonly IConfiguration _configuration;

        public EmployeeTypeController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // =====================================================
        // GET: EmployeeType/ChangeEmployeeType
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> ChangeEmployeeType()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            var model = new EmployeeTypeChangeViewModel
            {
                Operator = HttpContext.Session.GetString("LoginId") ?? "",
                Effect_Date = DateTime.Today
            };

            await LoadEmployees(model);
            await LoadHistory(model);

            return View(model);
        }


        // =====================================================
        // POST: SAVE EMPLOYEE TYPE
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(
            EmployeeTypeChangeViewModel model)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            string operatorCode =
                HttpContext.Session.GetString("LoginId")?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(operatorCode))
                return RedirectToAction("Login", "Account");

            if (model.EmpCode <= 0)
            {
                TempData["Error"] = "Please select Employee.";
                return RedirectToAction(nameof(ChangeEmployeeType));
            }

            if (model.Change_Type <= 0 || model.Change_Type > 7)
            {
                TempData["Error"] = "Please select a valid Employee Type.";
                return RedirectToAction(nameof(ChangeEmployeeType));
            }

            if (model.Effect_Date == default)
            {
                TempData["Error"] = "Please select Effect Date.";
                return RedirectToAction(nameof(ChangeEmployeeType));
            }

            if (string.IsNullOrWhiteSpace(model.Supervisor))
            {
                TempData["Error"] = "Please enter Supervisor Login Code.";
                return RedirectToAction(nameof(ChangeEmployeeType));
            }

            string connectionString =
                _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection not found in appsettings.json.");

            await using var connection =
                new SqlConnection(connectionString);

            await connection.OpenAsync();

            await using var transaction =
                await connection.BeginTransactionAsync();

            try
            {
                // -------------------------------------------------
                // 1. VALIDATE SUPERVISOR LOGIN CODE
                // -------------------------------------------------

                await using (var supervisorCommand =
                    new SqlCommand(@"
                        SELECT COUNT(1)
                        FROM dbo.UserMast
                        WHERE LTRIM(RTRIM(ini)) = @Supervisor;",
                        connection, (SqlTransaction)transaction))
                {
                    supervisorCommand.Parameters.Add(
                        "@Supervisor", SqlDbType.VarChar, 50)
                        .Value = model.Supervisor.Trim();

                    int supervisorExists = Convert.ToInt32(
                        await supervisorCommand.ExecuteScalarAsync());

                    if (supervisorExists == 0)
                    {
                        throw new Exception(
                            "Invalid Supervisor Login Code.");
                    }
                }


                // -------------------------------------------------
                // 2. GET PREVIOUS TYPE FROM PAYMAST
                // -------------------------------------------------

                int previousType;

                await using (var employeeCommand =
                    new SqlCommand(@"
                        SELECT TRY_CONVERT(INT, [type])
                        FROM dbo.paymast WITH (UPDLOCK, HOLDLOCK)
                        WHERE code = @EmpCode;",
                        connection, (SqlTransaction)transaction))
                {
                    employeeCommand.Parameters.Add(
                        "@EmpCode", SqlDbType.Int)
                        .Value = model.EmpCode;

                    object? result =
                        await employeeCommand.ExecuteScalarAsync();

                    if (result == null || result == DBNull.Value)
                    {
                        throw new Exception(
                            "Employee Code not found or Previous Type is invalid.");
                    }

                    previousType = Convert.ToInt32(result);
                }


                // -------------------------------------------------
                // 3. CHECK PREVIOUS AND NEW TYPE
                // -------------------------------------------------

                if (previousType == model.Change_Type)
                {
                    throw new Exception(
                        "Previous Type and New Type cannot be the same.");
                }


                // -------------------------------------------------
                // 4. INSERT INTO EMPLOYEETYPECHANGE
                // -------------------------------------------------

                const string insertQuery = @"
                    INSERT INTO dbo.EmployeeTypeChange
                    (
                        Effect_Date,
                        EmpCode,
                        Previous_Type,
                        Change_Type,
                        [Operator],
                        Supervisor
                    )
                    VALUES
                    (
                        @EffectDate,
                        @EmpCode,
                        @PreviousType,
                        @ChangeType,
                        @Operator,
                        @Supervisor
                    );";

                await using (var insertCommand =
                    new SqlCommand(insertQuery,
                        connection, (SqlTransaction)transaction))
                {
                    insertCommand.Parameters.Add(
                        "@EffectDate", SqlDbType.Date)
                        .Value = model.Effect_Date.Date;

                    insertCommand.Parameters.Add(
                        "@EmpCode", SqlDbType.Int)
                        .Value = model.EmpCode;

                    insertCommand.Parameters.Add(
                        "@PreviousType", SqlDbType.Int)
                        .Value = previousType;

                    insertCommand.Parameters.Add(
                        "@ChangeType", SqlDbType.Int)
                        .Value = model.Change_Type;

                    insertCommand.Parameters.Add(
                        "@Operator", SqlDbType.VarChar, 50)
                        .Value = operatorCode;

                    insertCommand.Parameters.Add(
                        "@Supervisor", SqlDbType.VarChar, 50)
                        .Value = model.Supervisor.Trim();

                    await insertCommand.ExecuteNonQueryAsync();
                }


                // -------------------------------------------------
                // 5. UPDATE PAYMAST TYPE
                // -------------------------------------------------

                const string updateQuery = @"
                    UPDATE dbo.paymast
                    SET [type] = @ChangeType
                    WHERE code = @EmpCode;";

                await using (var updateCommand =
                    new SqlCommand(updateQuery,
                        connection, (SqlTransaction)transaction))
                {
                    updateCommand.Parameters.Add(
                        "@ChangeType", SqlDbType.Int)
                        .Value = model.Change_Type;

                    updateCommand.Parameters.Add(
                        "@EmpCode", SqlDbType.Int)
                        .Value = model.EmpCode;

                    int affectedRows =
                        await updateCommand.ExecuteNonQueryAsync();

                    if (affectedRows != 1)
                    {
                        throw new Exception(
                            "Employee Type Update Failed.");
                    }
                }


                // -------------------------------------------------
                // 6. COMMIT TRANSACTION
                // -------------------------------------------------

                await transaction.CommitAsync();

                TempData["Success"] =
                    "Employee Type Changed Successfully.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(ChangeEmployeeType));
        }


        // =====================================================
        // LOAD EMPLOYEE DROPDOWN
        // =====================================================

        private async Task LoadEmployees(
            EmployeeTypeChangeViewModel model)
        {
            string connectionString =
                _configuration.GetConnectionString("DefaultConnection")!;

            const string query = @"
                SELECT
                    code,
                    ISNULL(name, '') AS EmployeeName,
                    TRY_CONVERT(INT, [type]) AS CurrentType
                FROM dbo.paymast
                ORDER BY code;";

            await using var connection =
                new SqlConnection(connectionString);

            await connection.OpenAsync();

            await using var command =
                new SqlCommand(query, connection);

            await using var reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                model.Employees.Add(
                    new EmployeeTypeEmployeeOption
                    {
                        EmpCode = Convert.ToInt32(reader["code"]),

                        EmployeeName =
                            Convert.ToString(reader["EmployeeName"]) ?? "",

                        CurrentType =
                            reader["CurrentType"] == DBNull.Value
                                ? null
                                : Convert.ToInt32(reader["CurrentType"])
                    });
            }
        }


        // =====================================================
        // LOAD EMPLOYEE TYPE CHANGE HISTORY
        // =====================================================

        private async Task LoadHistory(
            EmployeeTypeChangeViewModel model)
        {
            string connectionString =
                _configuration.GetConnectionString("DefaultConnection")!;

            const string query = @"
                SELECT TOP 500
                    c.SrNum,
                    c.Effect_Date,
                    c.EmpCode,
                    c.Previous_Type,
                    c.Change_Type,
                    c.[Operator],
                    c.Supervisor,
                    ISNULL(p.name, '') AS EmployeeName
                FROM dbo.EmployeeTypeChange c
                LEFT JOIN dbo.paymast p
                    ON p.code = c.EmpCode
                ORDER BY c.SrNum DESC;";

            await using var connection =
                new SqlConnection(connectionString);

            await connection.OpenAsync();

            await using var command =
                new SqlCommand(query, connection);

            await using var reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                model.Records.Add(
                    new EmployeeTypeChangeRow
                    {
                        SrNum = Convert.ToInt32(reader["SrNum"]),

                        Effect_Date =
                            Convert.ToDateTime(reader["Effect_Date"]),

                        EmpCode =
                            Convert.ToInt32(reader["EmpCode"]),

                        Previous_Type =
                            Convert.ToInt32(reader["Previous_Type"]),

                        Change_Type =
                            Convert.ToInt32(reader["Change_Type"]),

                        Operator =
                            Convert.ToString(reader["Operator"]) ?? "",

                        Supervisor =
                            Convert.ToString(reader["Supervisor"]) ?? "",

                        EmployeeName =
                            Convert.ToString(reader["EmployeeName"]) ?? ""
                    });
            }
        }


        // =====================================================
        // LOGIN CHECK
        // =====================================================

        private bool IsLoggedIn()
        {
            return !string.IsNullOrWhiteSpace(
                HttpContext.Session.GetString("LoginId"));
        }
    }
}