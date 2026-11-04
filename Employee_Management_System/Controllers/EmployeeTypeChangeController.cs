using System.Data;
using System.Data.Common;
using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Employee_Management_System.Controllers
{
    public class EmployeeTypeChangeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmployeeTypeChangeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: EmployeeTypeChange
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            var model = await BuildViewModel();

            return View(model);
        }

        // POST: Save Employee Type
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(EmployeeTypeChangeViewModel model)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            string operatorLoginId =
                HttpContext.Session.GetString("LoginId")?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(operatorLoginId))
                return RedirectToAction("Login", "Account");

            if (model.EmpCode <= 0 ||
                model.Change_Type < 1 ||
                model.Change_Type > 7 ||
                model.Effect_Date == default ||
                string.IsNullOrWhiteSpace(model.Supervisor))
            {
                TempData["Error"] = "Please enter all required information.";
                return RedirectToAction(nameof(Index));
            }

            var connection = _context.Database.GetDbConnection();

            bool openedHere = connection.State != ConnectionState.Open;

            DbTransaction? transaction = null;

            try
            {
                if (openedHere)
                    await connection.OpenAsync();

                transaction = await connection.BeginTransactionAsync();

                // 1. Validate Supervisor Login ID
                using (var supervisorCommand = connection.CreateCommand())
                {
                    supervisorCommand.Transaction = transaction;

                    supervisorCommand.CommandText = @"
SELECT COUNT(1)
FROM dbo.UserMast
WHERE LTRIM(RTRIM(ini)) = @Supervisor;";

                    AddParameter(
                        supervisorCommand,
                        "@Supervisor",
                        model.Supervisor.Trim());

                    int supervisorExists = Convert.ToInt32(
                        await supervisorCommand.ExecuteScalarAsync());

                    if (supervisorExists == 0)
                        throw new Exception("Invalid Supervisor Login Code.");
                }

                // 2. Get Employee Previous Type from paymast
                int previousType;

                using (var employeeCommand = connection.CreateCommand())
                {
                    employeeCommand.Transaction = transaction;

                    employeeCommand.CommandText = @"
SELECT TRY_CONVERT(INT, [type])
FROM dbo.paymast
WHERE code = @EmpCode;";

                    AddParameter(
                        employeeCommand,
                        "@EmpCode",
                        model.EmpCode);

                    object? result =
                        await employeeCommand.ExecuteScalarAsync();

                    if (result == null || result == DBNull.Value)
                        throw new Exception("Employee Code not found.");

                    previousType = Convert.ToInt32(result);
                }

                // 3. Check same Type
                if (previousType == model.Change_Type)
                    throw new Exception(
                        "Previous Type and New Type cannot be the same.");

                // 4. Insert History
                using (var insertCommand = connection.CreateCommand())
                {
                    insertCommand.Transaction = transaction;

                    insertCommand.CommandText = @"
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

                    AddParameter(
                        insertCommand,
                        "@EffectDate",
                        model.Effect_Date.Date);

                    AddParameter(
                        insertCommand,
                        "@EmpCode",
                        model.EmpCode);

                    AddParameter(
                        insertCommand,
                        "@PreviousType",
                        previousType);

                    AddParameter(
                        insertCommand,
                        "@ChangeType",
                        model.Change_Type);

                    AddParameter(
                        insertCommand,
                        "@Operator",
                        operatorLoginId);

                    AddParameter(
                        insertCommand,
                        "@Supervisor",
                        model.Supervisor.Trim());

                    await insertCommand.ExecuteNonQueryAsync();
                }

                // 5. Update paymast
                using (var updateCommand = connection.CreateCommand())
                {
                    updateCommand.Transaction = transaction;

                    updateCommand.CommandText = @"
UPDATE dbo.paymast
SET [type] = @ChangeType
WHERE code = @EmpCode;";

                    AddParameter(
                        updateCommand,
                        "@ChangeType",
                        model.Change_Type);

                    AddParameter(
                        updateCommand,
                        "@EmpCode",
                        model.EmpCode);

                    int rows = await updateCommand.ExecuteNonQueryAsync();

                    if (rows != 1)
                        throw new Exception("Employee Type update failed.");
                }

                // 6. Commit
                await transaction.CommitAsync();

                TempData["Success"] =
                    "Employee Type successfully changed.";
            }
            catch (Exception ex)
            {
                if (transaction != null)
                    await transaction.RollbackAsync();

                TempData["Error"] = ex.Message;
            }
            finally
            {
                if (transaction != null)
                    await transaction.DisposeAsync();

                if (openedHere &&
                    connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // Load Employees and History
        private async Task<EmployeeTypeChangeViewModel> BuildViewModel()
        {
            var model = new EmployeeTypeChangeViewModel
            {
                Operator =
                    HttpContext.Session.GetString("LoginId") ?? ""
            };

            var connection = _context.Database.GetDbConnection();

            bool openedHere = connection.State != ConnectionState.Open;

            try
            {
                if (openedHere)
                    await connection.OpenAsync();

                // Employee Dropdown
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT
    code AS EmpCode,
    ISNULL(name, '') AS EmployeeName,
    TRY_CONVERT(INT, [type]) AS CurrentType
FROM dbo.paymast
ORDER BY code;";

                    using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        model.Employees.Add(
                            new EmployeeTypeEmployeeOption
                            {
                                EmpCode = Convert.ToInt32(reader["EmpCode"]),

                                EmployeeName =
                                    Convert.ToString(reader["EmployeeName"]) ?? "",

                                CurrentType =
                                    reader["CurrentType"] == DBNull.Value
                                        ? null
                                        : Convert.ToInt32(reader["CurrentType"])
                            });
                    }
                }

                // History
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
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

                    using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        model.Records.Add(
                            new EmployeeTypeChangeRow
                            {
                                SrNum = Convert.ToInt32(reader["SrNum"]),

                                Effect_Date =
                                    Convert.ToDateTime(reader["Effect_Date"]),

                                EmpCode = Convert.ToInt32(reader["EmpCode"]),

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
            }
            finally
            {
                if (openedHere &&
                    connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }

            return model;
        }

        private bool IsLoggedIn()
        {
            return !string.IsNullOrWhiteSpace(
                HttpContext.Session.GetString("LoginId"));
        }

        private static void AddParameter(
            DbCommand command,
            string name,
            object? value)
        {
            var parameter = command.CreateParameter();

            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;

            command.Parameters.Add(parameter);
        }
    }
}