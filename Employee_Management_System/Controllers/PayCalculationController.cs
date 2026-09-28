using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace Employee_Management_System.Controllers
{
    public class PayCalculationController : Controller
    {
        private readonly ApplicationDbContext _context;
        public PayCalculationController(ApplicationDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            return View(new PayCalculationViewModel
            {
                SelectedYear = DateTime.Now.Year,
                SelectedMonth = DateTime.Now.Month,
                Branches = await LoadBranches()
            });
        }

        private async Task<List<PayCalculationBranchViewModel>> LoadBranches()
        {
            var result = new List<PayCalculationBranchViewModel>();
            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;
            try
            {
                if (close) await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = @"
SELECT TRY_CONVERT(INT,b.code) BranchCode,
       ISNULL(b.name,'') BranchName,
       COUNT(p.code) EmployeeCount
FROM dbo.brncmast b
LEFT JOIN dbo.PayMast p
  ON TRY_CONVERT(INT,p.brnc_code)=TRY_CONVERT(INT,b.code)
GROUP BY b.code,b.name
ORDER BY TRY_CONVERT(INT,b.code);";
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (reader["BranchCode"] == DBNull.Value) continue;
                    result.Add(new PayCalculationBranchViewModel
                    {
                        BranchCode = Convert.ToInt32(reader["BranchCode"]),
                        BranchName = Convert.ToString(reader["BranchName"])?.Trim() ?? "",
                        EmployeeCount = Convert.ToInt32(reader["EmployeeCount"])
                    });
                }
            }
            finally
            {
                if (close && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
            return result;
        }

        [HttpGet]
        public async Task<IActionResult> GetPayCalculation(int year, int month, int branchCode)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, sessionExpired = true, message = "Login session expired." });

            if (year < 1900 || month < 1 || month > 12 || branchCode <= 0)
                return Json(new { success = false, message = "Please select valid Year, Month and Branch." });

            DateTime monthStart = new DateTime(year, month, 1);
            DateTime monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var employees = new List<EmployeeSalaryRowViewModel>();
            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;

            try
            {
                if (close) await connection.OpenAsync();

                // IMPORTANT: Headers ALWAYS come from the master tables.
                // Therefore every allowance/deduction is displayed even when it is not applied to an employee.
                var allowanceNames = await LoadAllAllowanceNames(connection);
                var deductionNames = await LoadAllDeductionNames(connection);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT p.code EmployeeCode,
       ISNULL(p.name,'') EmployeeName,
       TRY_CONVERT(INT,p.grad_Code) GradeCode,
       ISNULL(g.name,'') GradeName,
       ISNULL(TRY_CONVERT(FLOAT,p.basic),0) BasicSalary
FROM dbo.PayMast p
LEFT JOIN dbo.gradmast g
  ON TRY_CONVERT(INT,g.code)=TRY_CONVERT(INT,p.grad_Code)
WHERE TRY_CONVERT(INT,p.brnc_code)=@BranchCode
ORDER BY p.code;";
                    AddParameter(command, "@BranchCode", branchCode);
                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var employee = new EmployeeSalaryRowViewModel
                        {
                            EmployeeCode = Convert.ToInt32(reader["EmployeeCode"]),
                            EmployeeName = Convert.ToString(reader["EmployeeName"])?.Trim() ?? "",
                            GradeCode = NullableInt(reader["GradeCode"]),
                            GradeName = Convert.ToString(reader["GradeName"])?.Trim() ?? "",
                            BasicSalary = Number(reader["BasicSalary"])
                        };

                        // Pre-fill every master component with 0.00.
                        foreach (var name in allowanceNames) employee.Allowances[name] = 0;
                        foreach (var name in deductionNames) employee.Deductions[name] = 0;
                        employees.Add(employee);
                    }
                }

                foreach (var employee in employees)
                {
                    await LoadEmployeeAllowances(connection, employee, monthStart, monthEnd);
                    await LoadEmployeeDeductions(connection, employee, monthStart, monthEnd);

                    employee.TotalAllowance = Math.Round(employee.Allowances.Values.Sum(), 2);
                    employee.GrossSalary = Math.Round(employee.BasicSalary + employee.TotalAllowance, 2);

                    // Professional Tax: April-February = 200, March = 300.
                    SetProfessionalTax(employee, month);

                    employee.TotalDeduction = Math.Round(employee.Deductions.Values.Sum(), 2);
                    employee.NetSalary = Math.Round(employee.GrossSalary - employee.TotalDeduction, 2);
                }

                return Json(new
                {
                    success = true,
                    count = employees.Count,
                    allowanceNames,
                    deductionNames,
                    data = employees
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Unable to calculate salary. " + ex.Message });
            }
            finally
            {
                if (close && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }

        [HttpPost]
        public async Task<IActionResult> SavePayCalculation(int year, int month, int branchCode, bool forceSave = false)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, sessionExpired = true, message = "Login session expired." });

            if (year < 1900 || month < 1 || month > 12 || branchCode <= 0)
                return Json(new { success = false, message = "Please select valid Year, Month and Branch." });

            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;
            DbTransaction? transaction = null;

            try
            {
                if (close) await connection.OpenAsync();

                // If this branch/month already has stored employee payroll rows,
                // first return a warning. Save Again calls this action with forceSave=true.
                if (!forceSave)
                {
                    using var duplicateCommand = connection.CreateCommand();
                    duplicateCommand.CommandText = @"
SELECT COUNT(1)
FROM dbo.paytrn t
INNER JOIN dbo.PayMast p
    ON TRY_CONVERT(INT,p.code)=TRY_CONVERT(INT,t.code)
WHERE TRY_CONVERT(INT,t.[year])=@Year
  AND TRY_CONVERT(INT,t.[month])=@Month
  AND TRY_CONVERT(INT,t.brnc)=@BranchCode
  AND TRY_CONVERT(INT,p.brnc_code)=@BranchCode;";
                    AddParameter(duplicateCommand, "@Year", year);
                    AddParameter(duplicateCommand, "@Month", month);
                    AddParameter(duplicateCommand, "@BranchCode", branchCode);

                    int existingCount = Convert.ToInt32(await duplicateCommand.ExecuteScalarAsync() ?? 0);
                    if (existingCount > 0)
                    {
                        return Json(new
                        {
                            success = false,
                            alreadyStored = true,
                            existingCount,
                            message = "Payroll data is already stored for the selected Year, Month and Branch. Do you want to store it again?"
                        });
                    }
                }

                DateTime monthStart = new DateTime(year, month, 1);
                DateTime monthEnd = monthStart.AddMonths(1).AddDays(-1);
                int daysInMonth = DateTime.DaysInMonth(year, month);

                var allowanceNames = await LoadAllAllowanceNames(connection);
                var deductionNames = await LoadAllDeductionNames(connection);
                var employees = new List<EmployeeSalaryRowViewModel>();

                using (var employeeCommand = connection.CreateCommand())
                {
                    employeeCommand.CommandText = @"
SELECT p.code EmployeeCode,
       ISNULL(p.name,'') EmployeeName,
       TRY_CONVERT(INT,p.grad_Code) GradeCode,
       ISNULL(g.name,'') GradeName,
       ISNULL(TRY_CONVERT(FLOAT,p.basic),0) BasicSalary
FROM dbo.PayMast p
LEFT JOIN dbo.gradmast g
  ON TRY_CONVERT(INT,g.code)=TRY_CONVERT(INT,p.grad_Code)
WHERE TRY_CONVERT(INT,p.brnc_code)=@BranchCode
ORDER BY p.code;";
                    AddParameter(employeeCommand, "@BranchCode", branchCode);

                    using var reader = await employeeCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var employee = new EmployeeSalaryRowViewModel
                        {
                            EmployeeCode = Convert.ToInt32(reader["EmployeeCode"]),
                            EmployeeName = Convert.ToString(reader["EmployeeName"])?.Trim() ?? "",
                            GradeCode = NullableInt(reader["GradeCode"]),
                            GradeName = Convert.ToString(reader["GradeName"])?.Trim() ?? "",
                            BasicSalary = Number(reader["BasicSalary"])
                        };

                        foreach (var name in allowanceNames) employee.Allowances[name] = 0;
                        foreach (var name in deductionNames) employee.Deductions[name] = 0;
                        employees.Add(employee);
                    }
                }

                if (employees.Count == 0)
                    return Json(new { success = false, message = "No employees found for the selected branch." });

                foreach (var employee in employees)
                {
                    await LoadEmployeeAllowances(connection, employee, monthStart, monthEnd);
                    await LoadEmployeeDeductions(connection, employee, monthStart, monthEnd);

                    employee.TotalAllowance = Math.Round(employee.Allowances.Values.Sum(), 2);
                    employee.GrossSalary = Math.Round(employee.BasicSalary + employee.TotalAllowance, 2);

                    // Professional Tax: April-February = 200, March = 300.
                    SetProfessionalTax(employee, month);

                    employee.TotalDeduction = Math.Round(employee.Deductions.Values.Sum(), 2);
                    employee.NetSalary = Math.Round(employee.GrossSalary - employee.TotalDeduction, 2);
                }

                transaction = await connection.BeginTransactionAsync();

                foreach (var employee in employees)
                {
                    // paytrn stores DA and HRA separately.
                    double da = FindComponentAmount(employee.Allowances, "DEARNESS ALLOWANCE");
                    double hra = FindComponentAmount(employee.Allowances, "HOUSE RENT ALLOWANCE");

                    // type is NOT NULL in paytrn, so fetch the employee type from PayMast.
                    int employeeType;
                    using (var typeCommand = connection.CreateCommand())
                    {
                        typeCommand.Transaction = transaction;
                        typeCommand.CommandText = @"
SELECT ISNULL(TRY_CONVERT(INT,[type]),0)
FROM dbo.PayMast
WHERE TRY_CONVERT(INT,code)=@EmployeeCode;";
                        AddParameter(typeCommand, "@EmployeeCode", employee.EmployeeCode);
                        employeeType = Convert.ToInt32(await typeCommand.ExecuteScalarAsync() ?? 0);
                    }

                    using var insertCommand = connection.CreateCommand();
                    insertCommand.Transaction = transaction;
                    insertCommand.CommandText = @"
INSERT INTO dbo.paytrn
(
    [year],[month],[code],[type],[basic],[da],[hra],[grad],[brnc],
    [tot_allw],[tot_dedu],[issue_date],[post],[orgn_basic],[days],
    [index],[Bank_PF],[POST_FLAG],[basic_org],[da_org],[hra_org]
)
VALUES
(
    @Year,@Month,@Code,@Type,@Basic,@DA,@HRA,@Grad,@Branch,
    @TotalAllowance,@TotalDeduction,@IssueDate,NULL,@OriginalBasic,@Days,
    0,0,'N',NULL,NULL,NULL
);";

                    AddParameter(insertCommand, "@Year", year);
                    AddParameter(insertCommand, "@Month", month);
                    AddParameter(insertCommand, "@Code", employee.EmployeeCode);
                    AddParameter(insertCommand, "@Type", employeeType);
                    AddParameter(insertCommand, "@Basic", employee.BasicSalary);
                    AddParameter(insertCommand, "@DA", da);
                    AddParameter(insertCommand, "@HRA", hra);
                    AddParameter(insertCommand, "@Grad", employee.GradeCode);
                    AddParameter(insertCommand, "@Branch", branchCode);
                    AddParameter(insertCommand, "@TotalAllowance", employee.TotalAllowance);
                    AddParameter(insertCommand, "@TotalDeduction", employee.TotalDeduction);
                    AddParameter(insertCommand, "@IssueDate", monthEnd);
                    AddParameter(insertCommand, "@OriginalBasic", employee.BasicSalary);
                    AddParameter(insertCommand, "@Days", daysInMonth);

                    await insertCommand.ExecuteNonQueryAsync();

                    // Save allowance/deduction details in dbo.paytrnft.
                    await SavePayTrnFoot(connection, transaction, year, month, employee, employeeType, monthStart, monthEnd);
                }

                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    savedCount = employees.Count,
                    message = $"Pay calculation saved successfully for {employees.Count} employee(s)."
                });
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    try { await transaction.RollbackAsync(); } catch { }
                }

                return Json(new { success = false, message = "Unable to save Pay Calculation. " + ex.Message });
            }
            finally
            {
                if (transaction != null)
                    await transaction.DisposeAsync();

                if (close && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }

        private static void SetProfessionalTax(EmployeeSalaryRowViewModel employee, int month)
        {
            double professionalTax = month == 3 ? 300.0 : 200.0;
            string? key = employee.Deductions.Keys
                .FirstOrDefault(x => x.Equals("PROFESSIONAL TAX", StringComparison.OrdinalIgnoreCase));

            if (key != null) employee.Deductions[key] = professionalTax;
            else employee.Deductions["PROFESSIONAL TAX"] = professionalTax;
        }

        private async Task SavePayTrnFoot(DbConnection connection, DbTransaction transaction, int year, int month,
            EmployeeSalaryRowViewModel employee, int employeeType, DateTime monthStart, DateTime monthEnd)
        {
            // Allowances from payfoot (flag=1).
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
SELECT TRY_CONVERT(INT,pf.allw_dedu) ComponentCode,
       ISNULL(TRY_CONVERT(FLOAT,pf.amt),0) FixedAmount,
       ISNULL(TRY_CONVERT(FLOAT,pf.[percent]),0) [Percent]
FROM dbo.payfoot pf
WHERE TRY_CONVERT(INT,pf.empl_code)=@EmployeeCode
  AND TRY_CONVERT(INT,pf.flag)=1
  AND (pf.[date] IS NULL OR pf.[date] <= @MonthEnd)
  AND (pf.cancel_date IS NULL OR pf.cancel_date >= @MonthStart)
ORDER BY TRY_CONVERT(INT,pf.allw_dedu);";
                AddParameter(cmd, "@EmployeeCode", employee.EmployeeCode);
                AddParameter(cmd, "@MonthStart", monthStart);
                AddParameter(cmd, "@MonthEnd", monthEnd);
                var rows = new List<(int Code, double Amount)>();
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                    {
                        if (r["ComponentCode"] == DBNull.Value) continue;
                        rows.Add((Convert.ToInt32(r["ComponentCode"]), Calculate(employee.BasicSalary, Number(r["FixedAmount"]), Number(r["Percent"]))));
                    }
                foreach (var row in rows)
                    await InsertPayTrnFootRow(connection, transaction, year, month, employee.EmployeeCode, 1, employeeType, row.Code, row.Amount);
            }

            // Deductions from payfoot (flag=2), except Professional Tax which is automatic.
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
SELECT TRY_CONVERT(INT,pf.allw_dedu) ComponentCode, LTRIM(RTRIM(ISNULL(dm.name,''))) ComponentName,
       ISNULL(TRY_CONVERT(FLOAT,pf.amt),0) FixedAmount, ISNULL(TRY_CONVERT(FLOAT,pf.[percent]),0) [Percent]
FROM dbo.payfoot pf
INNER JOIN dbo.dedumast dm ON TRY_CONVERT(INT,dm.code)=TRY_CONVERT(INT,pf.allw_dedu)
WHERE TRY_CONVERT(INT,pf.empl_code)=@EmployeeCode
  AND TRY_CONVERT(INT,pf.flag)=2
  AND (pf.[date] IS NULL OR pf.[date] <= @MonthEnd)
  AND (pf.cancel_date IS NULL OR pf.cancel_date >= @MonthStart)
ORDER BY TRY_CONVERT(INT,pf.allw_dedu);";
                AddParameter(cmd, "@EmployeeCode", employee.EmployeeCode);
                AddParameter(cmd, "@MonthStart", monthStart);
                AddParameter(cmd, "@MonthEnd", monthEnd);
                var rows = new List<(int Code, string Name, double Amount)>();
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                    {
                        if (r["ComponentCode"] == DBNull.Value) continue;
                        rows.Add((Convert.ToInt32(r["ComponentCode"]), Convert.ToString(r["ComponentName"])?.Trim() ?? "",
                            Calculate(employee.BasicSalary, Number(r["FixedAmount"]), Number(r["Percent"]))));
                    }
                foreach (var row in rows)
                    if (!row.Name.Equals("PROFESSIONAL TAX", StringComparison.OrdinalIgnoreCase))
                        await InsertPayTrnFootRow(connection, transaction, year, month, employee.EmployeeCode, 2, employeeType, row.Code, row.Amount);
            }

            int? ptCode = null;
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"SELECT TOP 1 TRY_CONVERT(INT,code) FROM dbo.dedumast
WHERE UPPER(LTRIM(RTRIM(ISNULL(name,''))))='PROFESSIONAL TAX' ORDER BY TRY_CONVERT(INT,code);";
                var result = await cmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value) ptCode = Convert.ToInt32(result);
            }
            if (!ptCode.HasValue)
                throw new InvalidOperationException("PROFESSIONAL TAX is not available in dbo.dedumast.");

            await InsertPayTrnFootRow(connection, transaction, year, month, employee.EmployeeCode, 2, employeeType,
                ptCode.Value, month == 3 ? 300.0 : 200.0);
        }

        private async Task InsertPayTrnFootRow(DbConnection connection, DbTransaction transaction, int year, int month,
            int employeeCode, int flag, int employeeType, int componentCode, double amount)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
INSERT INTO dbo.paytrnft ([year],[month],[empl],[flag],[type],[allw_dedu_code],[amt],[amt_org],[Entry_Date])
VALUES (@Year,@Month,@EmployeeCode,@Flag,@Type,@ComponentCode,@Amount,@Amount,GETDATE());";
            AddParameter(cmd, "@Year", year);
            AddParameter(cmd, "@Month", month);
            AddParameter(cmd, "@EmployeeCode", employeeCode);
            AddParameter(cmd, "@Flag", flag);
            AddParameter(cmd, "@Type", employeeType);
            AddParameter(cmd, "@ComponentCode", componentCode);
            AddParameter(cmd, "@Amount", Math.Round(amount, 2));
            await cmd.ExecuteNonQueryAsync();
        }

        private static double FindComponentAmount(Dictionary<string, double> components, string componentName)
        {
            foreach (var item in components)
            {
                if (item.Key.Equals(componentName, StringComparison.OrdinalIgnoreCase))
                    return Math.Round(item.Value, 2);
            }

            return 0;
        }

        private async Task<List<string>> LoadAllAllowanceNames(DbConnection connection)
        {
            var result = new List<string>();
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT LTRIM(RTRIM(ISNULL(name,''))) AllowanceName
FROM dbo.allwmast
WHERE ISNULL(LTRIM(RTRIM(name)),'') <> ''
ORDER BY TRY_CONVERT(INT,code);";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string name = Convert.ToString(reader["AllowanceName"])?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(name) && !result.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    result.Add(name);
            }
            return result;
        }

        private async Task<List<string>> LoadAllDeductionNames(DbConnection connection)
        {
            var result = new List<string>();
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT LTRIM(RTRIM(ISNULL(name,''))) DeductionName
FROM dbo.dedumast
WHERE ISNULL(LTRIM(RTRIM(name)),'') <> ''
ORDER BY TRY_CONVERT(INT,code);";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string name = Convert.ToString(reader["DeductionName"])?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(name) && !result.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    result.Add(name);
            }
            return result;
        }

        private async Task LoadEmployeeAllowances(DbConnection connection, EmployeeSalaryRowViewModel employee, DateTime monthStart, DateTime monthEnd)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT LTRIM(RTRIM(ISNULL(am.name,''))) ComponentName,
       ISNULL(TRY_CONVERT(FLOAT,pf.amt),0) Amount,
       ISNULL(TRY_CONVERT(FLOAT,pf.[percent]),0) [Percent]
FROM dbo.payfoot pf
INNER JOIN dbo.allwmast am
  ON TRY_CONVERT(INT,am.code)=TRY_CONVERT(INT,pf.allw_dedu)
WHERE TRY_CONVERT(INT,pf.empl_code)=@EmployeeCode
  AND TRY_CONVERT(INT,pf.flag)=1
  AND (pf.[date] IS NULL OR pf.[date] <= @MonthEnd)
  AND (pf.cancel_date IS NULL OR pf.cancel_date >= @MonthStart)
ORDER BY TRY_CONVERT(INT,pf.allw_dedu);";
            AddParameter(command, "@EmployeeCode", employee.EmployeeCode);
            AddParameter(command, "@MonthStart", monthStart);
            AddParameter(command, "@MonthEnd", monthEnd);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string name = Convert.ToString(reader["ComponentName"])?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;

                double fixedAmount = Number(reader["Amount"]);
                double percent = Number(reader["Percent"]);
                double amount = Calculate(employee.BasicSalary, fixedAmount, percent);

                if (employee.Allowances.ContainsKey(name))
                    employee.Allowances[name] += amount;
                else
                    employee.Allowances[name] = amount;
            }
        }

        private async Task LoadEmployeeDeductions(DbConnection connection, EmployeeSalaryRowViewModel employee, DateTime monthStart, DateTime monthEnd)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT LTRIM(RTRIM(ISNULL(dm.name,''))) ComponentName,
       ISNULL(TRY_CONVERT(FLOAT,pf.amt),0) Amount,
       ISNULL(TRY_CONVERT(FLOAT,pf.[percent]),0) [Percent]
FROM dbo.payfoot pf
INNER JOIN dbo.dedumast dm
  ON TRY_CONVERT(INT,dm.code)=TRY_CONVERT(INT,pf.allw_dedu)
WHERE TRY_CONVERT(INT,pf.empl_code)=@EmployeeCode
  AND TRY_CONVERT(INT,pf.flag)=2
  AND (pf.[date] IS NULL OR pf.[date] <= @MonthEnd)
  AND (pf.cancel_date IS NULL OR pf.cancel_date >= @MonthStart)
ORDER BY TRY_CONVERT(INT,pf.allw_dedu);";
            AddParameter(command, "@EmployeeCode", employee.EmployeeCode);
            AddParameter(command, "@MonthStart", monthStart);
            AddParameter(command, "@MonthEnd", monthEnd);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string name = Convert.ToString(reader["ComponentName"])?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;

                double fixedAmount = Number(reader["Amount"]);
                double percent = Number(reader["Percent"]);
                double amount = Calculate(employee.BasicSalary, fixedAmount, percent);

                if (employee.Deductions.ContainsKey(name))
                    employee.Deductions[name] += amount;
                else
                    employee.Deductions[name] = amount;
            }
        }

        // Current agreed rule: if payfoot.amt > 0 use that amount;
        // otherwise calculate payfoot.percent on Basic Salary.
        private static double Calculate(double basic, double fixedAmount, double percent)
        {
            double amount = fixedAmount > 0
                ? fixedAmount
                : percent > 0 ? basic * percent / 100.0 : 0;
            return Math.Round(amount, 2);
        }

        private bool IsLoggedIn() =>
            !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("LoginId"));

        private static void AddParameter(DbCommand command, string name, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private static int? NullableInt(object value) =>
            value == DBNull.Value ? null : Convert.ToInt32(value);

        private static double Number(object value) =>
            value == DBNull.Value ? 0 : Convert.ToDouble(value);
    }
}
