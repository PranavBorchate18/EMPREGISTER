using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;

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

            double professionalTax = await LoadProfessionalTax();

            return View(new PayCalculationViewModel
            {
                SelectedYear = DateTime.Now.Year,
                SelectedMonth = DateTime.Now.Month,
                ProfessionalTax = professionalTax,
                Branches = await LoadBranches()
            });
        }

        // ==========================================
        // PROFESSIONAL TAX - dbo.daptsys.from1
        // ==========================================
        private async Task<double> LoadProfessionalTax()
        {
            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;

            try
            {
                if (close) await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"SELECT TOP 1
       ISNULL(TRY_CONVERT(FLOAT, [from1]), 0)
FROM dbo.daptsys;";

                var result = await command.ExecuteScalarAsync();
                return result == null || result == DBNull.Value
                    ? 0
                    : Convert.ToDouble(result);
            }
            finally
            {
                if (close && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }

        // ==========================================
        // UPDATE PROFESSIONAL TAX - dbo.daptsys.from1
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> UpdateProfessionalTax(double professionalTax)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, sessionExpired = true, message = "Login session expired." });

            if (professionalTax < 0)
                return Json(new { success = false, message = "Please enter a valid Professional Tax value." });

            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;

            try
            {
                if (close) await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
UPDATE dbo.daptsys
SET [from1] = @ProfessionalTax;";

                AddParameter(command, "@ProfessionalTax", professionalTax);

                int affectedRows = await command.ExecuteNonQueryAsync();

                if (affectedRows <= 0)
                    return Json(new { success = false, message = "Professional Tax record was not found in dbo.daptsys." });

                return Json(new
                {
                    success = true,
                    value = professionalTax,
                    message = "Professional Tax updated successfully."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Unable to update Professional Tax. " + ex.Message });
            }
            finally
            {
                if (close && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
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

            // Employees having saved payroll for the selected Year + Month.
            // Saved Basic/details are used directly, so WPL is not applied twice.
            var savedPayrollEmployees = new HashSet<int>();

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
       ISNULL(TRY_CONVERT(FLOAT,p.basic),0) OriginalBasicSalary,
       CASE WHEN t.code IS NULL
            THEN ISNULL(TRY_CONVERT(FLOAT,p.basic),0)
            ELSE ISNULL(TRY_CONVERT(FLOAT,t.basic),0) END BasicSalary,
       CASE WHEN t.code IS NULL THEN 0 ELSE 1 END HasSavedPayroll,
       ISNULL(TRY_CONVERT(FLOAT,t.tot_allw),0) SavedTotalAllowance,
       ISNULL(TRY_CONVERT(FLOAT,t.tot_dedu),0) SavedTotalDeduction,
       ISNULL(TRY_CONVERT(FLOAT,t.days),0) SavedDays
FROM dbo.PayMast p
LEFT JOIN dbo.gradmast g
  ON TRY_CONVERT(INT,g.code)=TRY_CONVERT(INT,p.grad_Code)
LEFT JOIN dbo.paytrn t
  ON TRY_CONVERT(INT,t.code)=TRY_CONVERT(INT,p.code)
 AND TRY_CONVERT(INT,t.[year])=@Year
 AND TRY_CONVERT(INT,t.[month])=@Month
WHERE TRY_CONVERT(INT,p.brnc_code)=@BranchCode
ORDER BY p.code;";
                    AddParameter(command, "@BranchCode", branchCode);
                    AddParameter(command, "@Year", year);
                    AddParameter(command, "@Month", month);
                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var employee = new EmployeeSalaryRowViewModel
                        {
                            EmployeeCode = Convert.ToInt32(reader["EmployeeCode"]),
                            EmployeeName = Convert.ToString(reader["EmployeeName"])?.Trim() ?? "",
                            GradeCode = NullableInt(reader["GradeCode"]),
                            GradeName = Convert.ToString(reader["GradeName"])?.Trim() ?? "",
                            OriginalBasicSalary = Number(reader["OriginalBasicSalary"]),
                            BasicSalary = Number(reader["BasicSalary"])
                        };

                        if (Convert.ToInt32(reader["HasSavedPayroll"]) == 1)
                            savedPayrollEmployees.Add(employee.EmployeeCode);

                        // Pre-fill every master component with 0.00.
                        foreach (var name in allowanceNames) employee.Allowances[name] = 0;
                        foreach (var name in deductionNames) employee.Deductions[name] = 0;
                        employees.Add(employee);
                    }
                }

                double professionalTaxThreshold = await LoadProfessionalTax();

                foreach (var employee in employees)
                {
                    if (savedPayrollEmployees.Contains(employee.EmployeeCode))
                    {
                        // Saved month: show the exact payroll already stored by Salary Report/Pay Calculation.
                        // Basic comes from dbo.paytrn; individual allowance/deduction values come from dbo.paytrnft.
                        await LoadSavedPayTrnFtValues(connection, employee, year, month);

                        // A formula may be configured after this month was originally saved.
                        // Re-evaluate ONLY formula-based allowances using the Basic currently
                        // displayed from dbo.paytrn. Normal saved allowances remain untouched.
                        await RefreshFormulaAllowances(connection, employee, monthStart, monthEnd);

                        employee.TotalAllowance = Math.Round(employee.Allowances.Values.Sum(), 2);
                        employee.GrossSalary = Math.Round(employee.BasicSalary + employee.TotalAllowance, 2);

                        // PT depends on Gross, so formula allowance must affect it immediately.
                        SetProfessionalTax(employee, professionalTaxThreshold, month);

                        employee.TotalDeduction = Math.Round(employee.Deductions.Values.Sum(), 2);
                        employee.NetSalary = Math.Round(employee.GrossSalary - employee.TotalDeduction, 2);
                    }
                    else
                    {
                        // Unsaved month: keep the existing live Pay Calculation logic.
                        ApplyWithoutPayLeave(
                            employee,
                            await LoadApprovedWithoutPayLeaveDays(connection, employee.EmployeeCode, monthStart, monthEnd),
                            year, month);

                        await LoadEmployeeAllowances(connection, employee, monthStart, monthEnd);
                        await LoadEmployeeDeductions(connection, employee, monthStart, monthEnd);

                        employee.TotalAllowance = Math.Round(employee.Allowances.Values.Sum(), 2);
                        employee.GrossSalary = Math.Round(employee.BasicSalary + employee.TotalAllowance, 2);
                        SetProfessionalTax(employee, professionalTaxThreshold, month);
                        employee.TotalDeduction = Math.Round(employee.Deductions.Values.Sum(), 2);
                        employee.NetSalary = Math.Round(employee.GrossSalary - employee.TotalDeduction, 2);
                    }
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

                // Check by employees currently belonging to the selected branch.
                // Do not depend on paytrn.brnc, because older rows may contain a different/null branch value.
                // The paytrn primary key is based on Year + Month + Employee + Type.
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
                            OriginalBasicSalary = Number(reader["BasicSalary"]),
                            BasicSalary = Number(reader["BasicSalary"])
                        };

                        foreach (var name in allowanceNames) employee.Allowances[name] = 0;
                        foreach (var name in deductionNames) employee.Deductions[name] = 0;
                        employees.Add(employee);
                    }
                }

                if (employees.Count == 0)
                    return Json(new { success = false, message = "No employees found for the selected branch." });

                double professionalTaxThreshold = await LoadProfessionalTax();

                foreach (var employee in employees)
                {
                    ApplyWithoutPayLeave(
                        employee,
                        await LoadApprovedWithoutPayLeaveDays(connection, employee.EmployeeCode, monthStart, monthEnd),
                        year, month);

                    await LoadEmployeeAllowances(connection, employee, monthStart, monthEnd);
                    await LoadEmployeeDeductions(connection, employee, monthStart, monthEnd);

                    employee.TotalAllowance = Math.Round(employee.Allowances.Values.Sum(), 2);
                    employee.GrossSalary = Math.Round(employee.BasicSalary + employee.TotalAllowance, 2);

                    // Gross Salary below dbo.daptsys.from1 = 0
                    // Gross Salary >= threshold: March = 300, all other months = 200
                    SetProfessionalTax(employee, professionalTaxThreshold, month);

                    employee.TotalDeduction = Math.Round(employee.Deductions.Values.Sum(), 2);
                    employee.NetSalary = Math.Round(employee.GrossSalary - employee.TotalDeduction, 2);
                }

                transaction = await connection.BeginTransactionAsync();

                // Save Again = replace only the selected Year/Month/Branch payroll.
                // Delete detail rows first, then summary rows, to avoid duplicate primary-key errors.
                if (forceSave)
                {
                    using (var deleteFootCommand = connection.CreateCommand())
                    {
                        deleteFootCommand.Transaction = transaction;
                        deleteFootCommand.CommandText = @"
DELETE ft
FROM dbo.paytrnft ft
INNER JOIN dbo.PayMast p
    ON TRY_CONVERT(INT,p.code)=TRY_CONVERT(INT,ft.empl)
WHERE TRY_CONVERT(INT,ft.[year])=@Year
  AND TRY_CONVERT(INT,ft.[month])=@Month
  AND TRY_CONVERT(INT,p.brnc_code)=@BranchCode;";
                        AddParameter(deleteFootCommand, "@Year", year);
                        AddParameter(deleteFootCommand, "@Month", month);
                        AddParameter(deleteFootCommand, "@BranchCode", branchCode);
                        await deleteFootCommand.ExecuteNonQueryAsync();
                    }

                    using (var deletePayCommand = connection.CreateCommand())
                    {
                        deletePayCommand.Transaction = transaction;
                        deletePayCommand.CommandText = @"
DELETE t
FROM dbo.paytrn t
INNER JOIN dbo.PayMast p
    ON TRY_CONVERT(INT,p.code)=TRY_CONVERT(INT,t.code)
WHERE TRY_CONVERT(INT,t.[year])=@Year
  AND TRY_CONVERT(INT,t.[month])=@Month
  AND TRY_CONVERT(INT,p.brnc_code)=@BranchCode;";
                        AddParameter(deletePayCommand, "@Year", year);
                        AddParameter(deletePayCommand, "@Month", month);
                        AddParameter(deletePayCommand, "@BranchCode", branchCode);
                        await deletePayCommand.ExecuteNonQueryAsync();
                    }
                }

                foreach (var employee in employees)
                {
                    // paytrn stores DA and HRA separately.
                    double da = FindComponentAmount(employee.Allowances, "DA", "DEARNESS ALLOWANCE");
                    double hra = FindComponentAmount(employee.Allowances, "HRA", "HOUSE RENT ALLOWANCE");

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
                    AddParameter(insertCommand, "@OriginalBasic", employee.OriginalBasicSalary);
                    AddParameter(insertCommand, "@Days", Math.Max(0, daysInMonth - employee.WithoutPayLeaveDays));

                    await insertCommand.ExecuteNonQueryAsync();

                    // Save allowance/deduction details in dbo.paytrnft.
                    await SavePayTrnFoot(connection, transaction, year, month, employee, employeeType, monthStart, monthEnd);
                }

                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    savedCount = employees.Count,
                    message = $"Pay calculation saved successfully for {employees.Count} employee(s) for {new DateTime(year, month, 1):MMMM yyyy}."
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

        // Load exact saved monthly allowance/deduction values from dbo.paytrnft.
        // This is used only when dbo.paytrn already contains the selected Employee + Year + Month.
        private async Task LoadSavedPayTrnFtValues(
            DbConnection connection, EmployeeSalaryRowViewModel employee, int year, int month)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
SELECT LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(am.short_name)),''),am.name))) ComponentName,
       SUM(ISNULL(TRY_CONVERT(FLOAT,ft.amt),0)) Amount
FROM dbo.paytrnft ft
INNER JOIN dbo.allwmast am
  ON TRY_CONVERT(INT,am.code)=TRY_CONVERT(INT,ft.allw_dedu_code)
WHERE TRY_CONVERT(INT,ft.empl)=@EmployeeCode
  AND TRY_CONVERT(INT,ft.[year])=@Year
  AND TRY_CONVERT(INT,ft.[month])=@Month
  AND TRY_CONVERT(INT,ft.flag)=1
GROUP BY LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(am.short_name)),''),am.name)));";
                AddParameter(cmd, "@EmployeeCode", employee.EmployeeCode);
                AddParameter(cmd, "@Year", year);
                AddParameter(cmd, "@Month", month);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    string name = Convert.ToString(r["ComponentName"])?.Trim() ?? "";
                    if (!string.IsNullOrWhiteSpace(name)) employee.Allowances[name] = Number(r["Amount"]);
                }
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
SELECT LTRIM(RTRIM(ISNULL(dm.name,''))) ComponentName,
       SUM(ISNULL(TRY_CONVERT(FLOAT,ft.amt),0)) Amount
FROM dbo.paytrnft ft
INNER JOIN dbo.dedumast dm
  ON TRY_CONVERT(INT,dm.code)=TRY_CONVERT(INT,ft.allw_dedu_code)
WHERE TRY_CONVERT(INT,ft.empl)=@EmployeeCode
  AND TRY_CONVERT(INT,ft.[year])=@Year
  AND TRY_CONVERT(INT,ft.[month])=@Month
  AND TRY_CONVERT(INT,ft.flag)=2
GROUP BY LTRIM(RTRIM(ISNULL(dm.name,'')));";
                AddParameter(cmd, "@EmployeeCode", employee.EmployeeCode);
                AddParameter(cmd, "@Year", year);
                AddParameter(cmd, "@Month", month);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    string name = Convert.ToString(r["ComponentName"])?.Trim() ?? "";
                    if (!string.IsNullOrWhiteSpace(name)) employee.Deductions[name] = Number(r["Amount"]);
                }
            }
        }

        private static void SetProfessionalTax(
            EmployeeSalaryRowViewModel employee,
            double professionalTaxThreshold,
            int month)
        {
            double professionalTax = 0;

            if (professionalTaxThreshold > 0 &&
                employee.GrossSalary >= professionalTaxThreshold)
            {
                professionalTax = month == 3 ? 300.0 : 200.0;
            }

            string? key = employee.Deductions.Keys
                .FirstOrDefault(x => x.Equals("PROFESSIONAL TAX", StringComparison.OrdinalIgnoreCase));

            if (key != null)
                employee.Deductions[key] = professionalTax;
            else
                employee.Deductions["PROFESSIONAL TAX"] = professionalTax;
        }

        private async Task SavePayTrnFoot(DbConnection connection, DbTransaction transaction, int year, int month,
            EmployeeSalaryRowViewModel employee, int employeeType, DateTime monthStart, DateTime monthEnd)
        {
            // ----------------------------------------------------------
            // Save allowance details.
            //
            // Normal allowances:
            //     employee payfoot (flag = 1)
            //
            // Formula allowances:
            //     selected grade rule in dbo.allwfoot (Apply = 1)
            //     NO payfoot row required.
            //
            // UNION prevents a formula allowance from being missed while
            // saving and prevents duplicate component codes.
            // ----------------------------------------------------------
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
SELECT q.ComponentCode,
       MAX(q.ComponentName) ComponentName
FROM
(
    SELECT TRY_CONVERT(INT,pf.allw_dedu) ComponentCode,
           LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(am.short_name)),''),am.name))) ComponentName
    FROM dbo.payfoot pf
    INNER JOIN dbo.allwmast am
      ON TRY_CONVERT(INT,am.code)=TRY_CONVERT(INT,pf.allw_dedu)
    WHERE TRY_CONVERT(INT,pf.empl_code)=@EmployeeCode
      AND TRY_CONVERT(INT,pf.flag)=1
      AND (pf.[date] IS NULL OR pf.[date] <= @MonthEnd)
      AND (pf.cancel_date IS NULL OR pf.cancel_date >= @MonthStart)

    UNION ALL

    SELECT TRY_CONVERT(INT,af.allw_code) ComponentCode,
           LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(am.short_name)),''),am.name))) ComponentName
    FROM dbo.allwfoot af
    INNER JOIN dbo.allwmast am
      ON TRY_CONVERT(INT,am.code)=TRY_CONVERT(INT,af.allw_code)
    WHERE TRY_CONVERT(INT,af.grad_code)=@GradeCode
      AND ISNULL(TRY_CONVERT(INT,af.Apply),0)=1
      AND NULLIF(LTRIM(RTRIM(CONVERT(NVARCHAR(MAX),af.formula))), '') IS NOT NULL
) q
WHERE q.ComponentCode IS NOT NULL
GROUP BY q.ComponentCode
ORDER BY q.ComponentCode;";

                AddParameter(cmd, "@EmployeeCode", employee.EmployeeCode);
                AddParameter(cmd, "@GradeCode", employee.GradeCode);
                AddParameter(cmd, "@MonthStart", monthStart);
                AddParameter(cmd, "@MonthEnd", monthEnd);

                var rows = new List<(int Code, string Name, double Amount)>();

                using (var r = await cmd.ExecuteReaderAsync())
                {
                    while (await r.ReadAsync())
                    {
                        if (r["ComponentCode"] == DBNull.Value) continue;

                        int componentCode = Convert.ToInt32(r["ComponentCode"]);
                        string componentName =
                            Convert.ToString(r["ComponentName"])?.Trim() ?? "";

                        double calculatedAmount = 0;

                        if (!string.IsNullOrWhiteSpace(componentName))
                        {
                            string? allowanceKey = employee.Allowances.Keys
                                .FirstOrDefault(x =>
                                    x.Equals(componentName, StringComparison.OrdinalIgnoreCase));

                            if (allowanceKey != null)
                                calculatedAmount = employee.Allowances[allowanceKey];
                        }

                        rows.Add((
                            componentCode,
                            componentName,
                            Math.Round(calculatedAmount, 2)));
                    }
                }

                foreach (var row in rows)
                {
                    await InsertPayTrnFootRow(
                        connection,
                        transaction,
                        year,
                        month,
                        employee.EmployeeCode,
                        1,
                        employeeType,
                        row.Code,
                        row.Amount);
                }
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

            // Save the exact Professional Tax amount already calculated above.
            // Below threshold = 0, April-February = 200, March = 300.
            string? professionalTaxKey = employee.Deductions.Keys
                .FirstOrDefault(x => x.Equals("PROFESSIONAL TAX", StringComparison.OrdinalIgnoreCase));

            double professionalTaxAmount = professionalTaxKey == null
                ? 0
                : employee.Deductions[professionalTaxKey];

            if (professionalTaxAmount > 0)
            {
                int? ptCode = null;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"SELECT TOP 1 TRY_CONVERT(INT,code) FROM dbo.dedumast
WHERE UPPER(LTRIM(RTRIM(ISNULL(name,''))))='PROFESSIONAL TAX' ORDER BY TRY_CONVERT(INT,code);";
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                        ptCode = Convert.ToInt32(result);
                }

                if (!ptCode.HasValue)
                    throw new InvalidOperationException("PROFESSIONAL TAX is not available in dbo.dedumast.");

                await InsertPayTrnFootRow(
                    connection,
                    transaction,
                    year,
                    month,
                    employee.EmployeeCode,
                    2,
                    employeeType,
                    ptCode.Value,
                    professionalTaxAmount);
            }
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

        private static double FindComponentAmount(Dictionary<string, double> components, params string[] componentNames)
        {
            foreach (var componentName in componentNames)
            {
                foreach (var item in components)
                {
                    if (item.Key.Equals(componentName, StringComparison.OrdinalIgnoreCase))
                        return Math.Round(item.Value, 2);
                }
            }

            return 0;
        }

        private async Task<List<string>> LoadAllAllowanceNames(DbConnection connection)
        {
            var result = new List<string>();
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(short_name)),''),name))) AllowanceName
FROM dbo.allwmast
WHERE ISNULL(LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(short_name)),''),name))),'') <> ''
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

        // ==========================================
        // WITHOUT PAY LEAVE (leavmast code = 10)
        // Only APPROVED leave (Status A/APPROVED) affects salary.
        // ==========================================
        private async Task<double> LoadApprovedWithoutPayLeaveDays(
            DbConnection connection, int employeeCode, DateTime monthStart, DateTime monthEnd)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT ISNULL(SUM(
    CASE
        WHEN CAST(sact_from_date AS date) >= @MonthStart
         AND CAST(sact_to_date AS date) <= @MonthEnd
         AND ISNULL(TRY_CONVERT(FLOAT,Num_of_days),0) > 0
        THEN TRY_CONVERT(FLOAT,Num_of_days)
        ELSE DATEDIFF(DAY,
             CASE WHEN CAST(sact_from_date AS date) < @MonthStart THEN @MonthStart ELSE CAST(sact_from_date AS date) END,
             CASE WHEN CAST(sact_to_date AS date) > @MonthEnd THEN @MonthEnd ELSE CAST(sact_to_date AS date) END) + 1
    END),0)
FROM dbo.leavtrn
WHERE TRY_CONVERT(INT,empl)=@EmployeeCode
  AND TRY_CONVERT(INT,sact_code)=10
  AND UPPER(LTRIM(RTRIM(ISNULL(Status,'')))) IN ('A','APPROVED')
  AND sact_from_date IS NOT NULL
  AND sact_to_date IS NOT NULL
  AND CAST(sact_from_date AS date) <= @MonthEnd
  AND CAST(sact_to_date AS date) >= @MonthStart;";
            AddParameter(command, "@EmployeeCode", employeeCode);
            AddParameter(command, "@MonthStart", monthStart.Date);
            AddParameter(command, "@MonthEnd", monthEnd.Date);
            return Number(await command.ExecuteScalarAsync());
        }

        private static void ApplyWithoutPayLeave(EmployeeSalaryRowViewModel employee, double wplDays, int year, int month)
        {
            int daysInMonth = DateTime.DaysInMonth(year, month);
            wplDays = Math.Max(0, Math.Min(wplDays, daysInMonth));
            employee.WithoutPayLeaveDays = Math.Round(wplDays, 2);
            double original = employee.OriginalBasicSalary;
            double perDay = daysInMonth > 0 ? original / daysInMonth : 0;
            employee.WithoutPayLeaveAmount = Math.Round(perDay * wplDays, 2);
            employee.BasicSalary = Math.Round(Math.Max(0, original - employee.WithoutPayLeaveAmount), 2);
        }

        private async Task LoadEmployeeAllowances(DbConnection connection, EmployeeSalaryRowViewModel employee, DateTime monthStart, DateTime monthEnd)
        {
            // ----------------------------------------------------------
            // PASS 1: Normal employee allowances continue to use payfoot.
            // If a selected grade rule has a Special Formula for the same
            // allowance, do not calculate that payfoot row here.
            // ----------------------------------------------------------
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT TRY_CONVERT(INT,pf.allw_dedu) ComponentCode,
       LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(am.short_name)),''),am.name))) ComponentName,
       ISNULL(TRY_CONVERT(FLOAT,pf.amt),0) Amount,
       ISNULL(TRY_CONVERT(FLOAT,pf.[percent]),0) [Percent]
FROM dbo.payfoot pf
INNER JOIN dbo.allwmast am
  ON TRY_CONVERT(INT,am.code)=TRY_CONVERT(INT,pf.allw_dedu)
WHERE TRY_CONVERT(INT,pf.empl_code)=@EmployeeCode
  AND TRY_CONVERT(INT,pf.flag)=1
  AND (pf.[date] IS NULL OR pf.[date] <= @MonthEnd)
  AND (pf.cancel_date IS NULL OR pf.cancel_date >= @MonthStart)
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.allwfoot af
      WHERE TRY_CONVERT(INT,af.allw_code)=TRY_CONVERT(INT,pf.allw_dedu)
        AND TRY_CONVERT(INT,af.grad_code)=@GradeCode
        AND ISNULL(TRY_CONVERT(INT,af.Apply),0)=1
        AND NULLIF(LTRIM(RTRIM(CONVERT(NVARCHAR(MAX),af.formula))), '') IS NOT NULL
  )
ORDER BY TRY_CONVERT(INT,pf.allw_dedu);";

                AddParameter(command, "@EmployeeCode", employee.EmployeeCode);
                AddParameter(command, "@GradeCode", employee.GradeCode);
                AddParameter(command, "@MonthStart", monthStart);
                AddParameter(command, "@MonthEnd", monthEnd);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string name = Convert.ToString(reader["ComponentName"])?.Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    double amount = Calculate(
                        employee.BasicSalary,
                        Number(reader["Amount"]),
                        Number(reader["Percent"]));

                    AddAllowanceAmount(employee, name, amount);
                }
            }

            // ----------------------------------------------------------
            // PASS 2: Special Formula allowances are GRADE-WISE.
            // Source of selection:
            // dbo.allwfoot.Apply = 1 for employee's current Grade.
            //
            // IMPORTANT:
            // No payfoot row is required for a formula allowance.
            // BASIC = the Basic currently calculated/displayed on this page.
            // ----------------------------------------------------------
            await ApplySelectedGradeFormulaAllowances(connection, employee);
        }

        private async Task ApplySelectedGradeFormulaAllowances(
            DbConnection connection,
            EmployeeSalaryRowViewModel employee)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT TRY_CONVERT(INT,af.allw_code) ComponentCode,
       LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(am.short_name)),''),am.name))) ComponentName,
       LTRIM(RTRIM(CONVERT(NVARCHAR(MAX),af.formula))) Formula
FROM dbo.allwfoot af
INNER JOIN dbo.allwmast am
  ON TRY_CONVERT(INT,am.code)=TRY_CONVERT(INT,af.allw_code)
WHERE TRY_CONVERT(INT,af.grad_code)=@GradeCode
  AND ISNULL(TRY_CONVERT(INT,af.Apply),0)=1
  AND NULLIF(LTRIM(RTRIM(CONVERT(NVARCHAR(MAX),af.formula))), '') IS NOT NULL
ORDER BY TRY_CONVERT(INT,af.allw_code);";

            AddParameter(command, "@GradeCode", employee.GradeCode);

            var rows = new List<(int Code, string Name, string Formula)>();

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    if (reader["ComponentCode"] == DBNull.Value) continue;

                    string name = Convert.ToString(reader["ComponentName"])?.Trim() ?? "";
                    string formula = Convert.ToString(reader["Formula"])?.Trim() ?? "";

                    if (string.IsNullOrWhiteSpace(name) ||
                        string.IsNullOrWhiteSpace(formula))
                        continue;

                    rows.Add((
                        Convert.ToInt32(reader["ComponentCode"]),
                        name,
                        formula));
                }
            }

            foreach (var row in rows)
            {
                double da = FindComponentAmount(
                    employee.Allowances, "DA", "DEARNESS ALLOWANCE");

                double hra = FindComponentAmount(
                    employee.Allowances, "HRA", "HOUSE RENT ALLOWANCE");

                double amount = EvaluateAllowanceFormula(
                    row.Formula,
                    employee.BasicSalary,
                    da,
                    hra);

                // Formula allowance replaces the 0/master placeholder.
                string? existingKey = employee.Allowances.Keys
                    .FirstOrDefault(x =>
                        x.Equals(row.Name, StringComparison.OrdinalIgnoreCase));

                if (existingKey != null)
                    employee.Allowances[existingKey] = Math.Round(amount, 2);
                else
                    employee.Allowances[row.Name] = Math.Round(amount, 2);
            }
        }

        // Recalculate only formula-based allowances for an already-saved month.
        // BASIC = the Basic currently displayed in Pay Calculation (dbo.paytrn.basic).
        // This lets a newly configured SP formula affect Total Allowance/Gross immediately
        // without recalculating or duplicating the other saved allowance values.
        private async Task RefreshFormulaAllowances(
            DbConnection connection,
            EmployeeSalaryRowViewModel employee,
            DateTime monthStart,
            DateTime monthEnd)
        {
            // Saved month still keeps normal saved paytrnft values.
            // Formula allowances, however, follow the CURRENT selected grade
            // configuration from dbo.allwfoot (Apply = 1).
            //
            // Therefore a formula allowance does not require payfoot.
            await ApplySelectedGradeFormulaAllowances(connection, employee);
        }

        private static void AddAllowanceAmount(
                    EmployeeSalaryRowViewModel employee,
                    string name,
                    double amount)
        {
            if (employee.Allowances.ContainsKey(name))
                employee.Allowances[name] += Math.Round(amount, 2);
            else
                employee.Allowances[name] = Math.Round(amount, 2);
        }

        private static double EvaluateAllowanceFormula(
            string formula,
            double basic,
            double da,
            double hra)
        {
            if (string.IsNullOrWhiteSpace(formula)) return 0;

            string expression = Regex.Replace(formula, @"\s+", "");

            expression = Regex.Replace(
                expression, @"\bBASIC\b",
                basic.ToString(CultureInfo.InvariantCulture),
                RegexOptions.IgnoreCase);

            expression = Regex.Replace(
                expression, @"\bHRA\b",
                hra.ToString(CultureInfo.InvariantCulture),
                RegexOptions.IgnoreCase);

            expression = Regex.Replace(
                expression, @"\bDA\b",
                da.ToString(CultureInfo.InvariantCulture),
                RegexOptions.IgnoreCase);

            // Do not execute arbitrary SQL/code. After replacing supported names,
            // only arithmetic characters are accepted.
            if (!Regex.IsMatch(expression, @"^[0-9\.\+\-\*\/\(\)]+$"))
                throw new InvalidOperationException(
                    "Allowance formula contains an unsupported value. " +
                    "Use only BASIC, DA, HRA, numbers and + - * / ( ).");

            try
            {
                var table = new DataTable
                {
                    Locale = CultureInfo.InvariantCulture
                };

                object value = table.Compute(expression, string.Empty);
                double result = Convert.ToDouble(value, CultureInfo.InvariantCulture);

                if (double.IsNaN(result) || double.IsInfinity(result))
                    throw new InvalidOperationException("Formula result is invalid.");

                return Math.Round(Math.Max(0, result), 2);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    "Unable to calculate allowance formula: " + formula, ex);
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
            return Math.Round(Math.Max(0, amount), 2);
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

        private static double Number(object value)
        {
            if (value == DBNull.Value) return 0;

            double number = Convert.ToDouble(value);
            return number < 0 ? 0 : number;
        }
    }
}
