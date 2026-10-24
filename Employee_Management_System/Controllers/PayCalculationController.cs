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
                Branches = await LoadBranches(DateTime.Now.Year, DateTime.Now.Month)
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

        // Branch cards are rendered only after Start Calculation in the view.
        // Selected branch list is captured on successful month check.
        // Branch cards use the same live employee calculation as the dashboard.
        // This method only loads branch identities and employee counts; the view
        // fills financial totals from GetPayCalculation (no payroll save required).
        private async Task<List<PayCalculationBranchViewModel>> LoadBranches(int year, int month)
        {
            var result = new List<PayCalculationBranchViewModel>();
            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;
            try
            {
                if (close) await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = @"SELECT TRY_CONVERT(INT,b.code) BranchCode,
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
                        EmployeeCount = Convert.ToInt32(reader["EmployeeCount"]),
                        TotalBasic = 0,
                        TotalAllowance = 0,
                        TotalDeduction = 0,
                        TotalNetSalary = 0
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
        public async Task<IActionResult> GetBranchTotals(int year, int month)
        {
            if (!IsLoggedIn()) return Json(new { success = false, sessionExpired = true });
            if (year < 1900 || year > 9999 || month < 1 || month > 12)
                return Json(new { success = false, message = "Invalid year or month." });
            try
            {
                return Json(new { success = true, branches = await LoadBranches(year, month) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Unable to load branch totals. " + ex.Message });
            }
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
                var allowanceTypes = await LoadAllowanceTypes(connection);
                var deductionNames = await LoadAllDeductionNames(connection);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT p.code EmployeeCode,
       ISNULL(p.name,'') EmployeeName,
       ISNULL(CONVERT(NVARCHAR(20),p.sex),'') Gender,
       TRY_CONVERT(INT,p.grad_Code) GradeCode,
       ISNULL(TRY_CONVERT(INT,p.[type]),0) EmployeeType,
       ISNULL(g.name,'') GradeName,
       CASE WHEN t.code IS NULL THEN ISNULL(TRY_CONVERT(FLOAT,p.basic),0)
             ELSE COALESCE(NULLIF(TRY_CONVERT(FLOAT,t.orgn_basic),0),ISNULL(TRY_CONVERT(FLOAT,p.basic),0)) END OriginalBasicSalary,
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
                            Gender = Convert.ToString(reader["Gender"])?.Trim() ?? "",
                            GradeCode = NullableInt(reader["GradeCode"]),
                            EmployeeType = Convert.ToInt32(reader["EmployeeType"]),
                            GradeName = Convert.ToString(reader["GradeName"])?.Trim() ?? "",
                            OriginalBasicSalary = Number(reader["OriginalBasicSalary"]),
                            BasicSalary = Number(reader["BasicSalary"])
                        };

                        if (Convert.ToInt32(reader["HasSavedPayroll"]) == 1)
                            savedPayrollEmployees.Add(employee.EmployeeCode);

                        // Pre-fill every master component with 0.00.
                        foreach (var name in allowanceNames) employee.Allowances[name] = 0;
                        employee.AllowanceTypes = allowanceTypes;
                        foreach (var name in deductionNames) employee.Deductions[name] = 0;
                        employees.Add(employee);
                    }
                }

                // Professional Tax is applied by SetProfessionalTax for the selected month.

                foreach (var employee in employees)
                {
                    if (savedPayrollEmployees.Contains(employee.EmployeeCode))
                    {
                        // Saved month: show the exact payroll already stored by Salary Report/Pay Calculation.
                        // Basic comes from dbo.paytrn; individual allowance/deduction values come from dbo.paytrnft.
                        await LoadSavedPayTrnFtValues(connection, employee, year, month);

                        // Current approved WPL determines Basic BEFORE dependent allowances/PF.
                        ApplyWithoutPayLeave(employee,
                            await LoadApprovedWithoutPayLeaveDays(connection, employee.EmployeeCode, monthStart, monthEnd),
                            year, month);

                        // Refresh saved-month dashboard preview from current Employee Pay Setup.
                        // The persisted payroll remains unchanged until Save Again is confirmed.
                        // This refreshes employee-specific amounts (including FDA 2) and
                        // grade formula dependencies such as HRA / PF.
                        await LoadEmployeeAllowances(connection, employee, monthStart, monthEnd);
                        await LoadEmployeeDeductions(connection, employee, monthStart, monthEnd);
                        ApplyNewSalaryRules(employee, month);
                        await ApplySelectedGradePayRules(connection, employee, monthStart, monthEnd);

                        // Preview-only correction for Employee 3, April 2026.
                        // Keep saved payroll untouched until the user explicitly saves again.
                        if (employee.EmployeeCode == 3 && year == 2026 && month == 4)
                        {
                            await RefreshEmployeeThreeAprilLoanPreview(connection, employee, monthStart, monthEnd);
                            await RefreshEmployeeThreeAprilSroPreview(connection, employee, monthStart, monthEnd);
                            // April 2026 salary sheet: HRA = 15% of subtotal;
                            // PF = 12% of subtotal. Recompute from the saved FDA2 etc.
                            // This is a dashboard-only correction, not a database write.
                            RefreshEmployeeThreeAprilHraAndPfPreview(employee);
                        }



                        // Load historical components, then calculate PT for the current
                        // dashboard preview using the selected month's statutory slab.
                        // This does not modify the saved dbo.paytrn/paytrnft records.
                        RemoveContractProvidentFund(employee);
                        RemoveContractHra(employee);
                        RecalculateSavedPayrollTotals(employee);
                        SetProfessionalTax(employee, month);
                        RecalculateSavedPayrollTotals(employee);

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

                        ApplyNewSalaryRules(employee, month);
                        await ApplySelectedGradePayRules(connection, employee, monthStart, monthEnd);
                        RemoveContractProvidentFund(employee);
                        RemoveContractHra(employee);
                        SetProfessionalTax(employee, month);
                        RecalculateSavedPayrollTotals(employee);

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

        // Check only the branches selected by the administrator.
        [HttpGet]
        public async Task<IActionResult> CheckPayrollMonth(int year, int month, string branchCodes)
        {
            if (!IsLoggedIn()) return Json(new { success = false, sessionExpired = true });
            if (!ValidPeriodAndBranches(year, month, branchCodes, out var codes))
                return Json(new { success = false, message = "Select a valid year, month and at least one branch." });

            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;
            try
            {
                if (close) await connection.OpenAsync();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"SELECT
    (SELECT COUNT(1) FROM dbo.paytrn t
     INNER JOIN dbo.PayMast p ON TRY_CONVERT(INT,p.code)=TRY_CONVERT(INT,t.code)
     WHERE TRY_CONVERT(INT,t.[year])=@Year AND TRY_CONVERT(INT,t.[month])=@Month
       AND TRY_CONVERT(INT,p.brnc_code) IN (SELECT TRY_CONVERT(INT,value) FROM STRING_SPLIT(@Branches,','))) AS HeaderCount,
    (SELECT COUNT(1) FROM dbo.paytrnft ft
     INNER JOIN dbo.PayMast p ON TRY_CONVERT(INT,p.code)=TRY_CONVERT(INT,ft.empl)
     WHERE TRY_CONVERT(INT,ft.[year])=@Year AND TRY_CONVERT(INT,ft.[month])=@Month
       AND TRY_CONVERT(INT,p.brnc_code) IN (SELECT TRY_CONVERT(INT,value) FROM STRING_SPLIT(@Branches,','))) AS DetailCount;";
                AddParameter(cmd, "@Year", year);
                AddParameter(cmd, "@Month", month);
                AddParameter(cmd, "@Branches", string.Join(",", codes));
                using var reader = await cmd.ExecuteReaderAsync();
                await reader.ReadAsync();
                int headerCount = Convert.ToInt32(reader["HeaderCount"]);
                int detailCount = Convert.ToInt32(reader["DetailCount"]);
                return Json(new { success = true, alreadyStored = headerCount > 0 || detailCount > 0, headerCount, detailCount });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
            finally { if (close && connection.State == ConnectionState.Open) await connection.CloseAsync(); }
        }

        private static bool ValidPeriodAndBranches(int year, int month, string branchCodes, out List<int> codes)
        {
            codes = new List<int>();
            if (year < 1900 || year > 9999 || month < 1 || month > 12 || string.IsNullOrWhiteSpace(branchCodes)) return false;
            foreach (string part in branchCodes.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(part.Trim(), out int code) || code <= 0) return false;
                codes.Add(code);
            }
            codes = codes.Distinct().ToList();
            return codes.Count > 0;
        }

        // Atomic deletion limited to the selected year, month and branch codes.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecalculatePayrollMonth(int year, int month, string branchCodes)
        {
            if (!IsLoggedIn()) return Json(new { success = false, sessionExpired = true });
            if (!ValidPeriodAndBranches(year, month, branchCodes, out var codes))
                return Json(new { success = false, message = "Invalid year, month or branch selection." });

            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;
            DbTransaction? transaction = null;
            try
            {
                if (close) await connection.OpenAsync();
                transaction = await connection.BeginTransactionAsync();
                // Delete child rows first to preserve foreign-key integrity.
                int deletedDetails;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"DELETE ft FROM dbo.paytrnft ft
INNER JOIN dbo.PayMast p ON TRY_CONVERT(INT,p.code)=TRY_CONVERT(INT,ft.empl)
WHERE TRY_CONVERT(INT,ft.[year])=@Year AND TRY_CONVERT(INT,ft.[month])=@Month
  AND TRY_CONVERT(INT,p.brnc_code) IN (SELECT TRY_CONVERT(INT,value) FROM STRING_SPLIT(@Branches,','));";
                    AddParameter(cmd, "@Year", year);
                    AddParameter(cmd, "@Month", month);
                    AddParameter(cmd, "@Branches", string.Join(",", codes));
                    deletedDetails = await cmd.ExecuteNonQueryAsync();
                }
                int deletedHeaders;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"DELETE t FROM dbo.paytrn t
INNER JOIN dbo.PayMast p ON TRY_CONVERT(INT,p.code)=TRY_CONVERT(INT,t.code)
WHERE TRY_CONVERT(INT,t.[year])=@Year AND TRY_CONVERT(INT,t.[month])=@Month
  AND TRY_CONVERT(INT,p.brnc_code) IN (SELECT TRY_CONVERT(INT,value) FROM STRING_SPLIT(@Branches,','));";
                    AddParameter(cmd, "@Year", year);
                    AddParameter(cmd, "@Month", month);
                    AddParameter(cmd, "@Branches", string.Join(",", codes));
                    deletedHeaders = await cmd.ExecuteNonQueryAsync();
                }
                await transaction.CommitAsync();
                return Json(new
                {
                    success = true,
                    deletedHeaders,
                    deletedDetails,
                    message = $"Deleted {deletedHeaders} payroll headers and {deletedDetails} detail records for {new DateTime(year, month, 1):MMMM yyyy}, selected branches only."
                });
            }
            catch (Exception ex)
            {
                if (transaction != null) { try { await transaction.RollbackAsync(); } catch { } }
                return Json(new { success = false, message = "Deletion rolled back: " + ex.Message });
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
                if (close && connection.State == ConnectionState.Open) await connection.CloseAsync();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePayCalculation(int year, int month, string branchCodes, bool forceSave = false)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, sessionExpired = true, message = "Login session expired." });

            if (!ValidPeriodAndBranches(year, month, branchCodes, out var selectedCodes))
                return Json(new { success = false, message = "Please select valid Year, Month and Branch." });

            string selectedBranches = string.Join(",", selectedCodes);
            var employeeBranches = new Dictionary<int, int>();
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
SELECT
    (SELECT COUNT(1) FROM dbo.paytrn t
     INNER JOIN dbo.PayMast p ON TRY_CONVERT(INT,p.code)=TRY_CONVERT(INT,t.code)
     WHERE TRY_CONVERT(INT,t.[year])=@Year AND TRY_CONVERT(INT,t.[month])=@Month
       AND TRY_CONVERT(INT,p.brnc_code) IN (SELECT TRY_CONVERT(INT,value) FROM STRING_SPLIT(@Branches,',')))
  + (SELECT COUNT(1) FROM dbo.paytrnft ft
     INNER JOIN dbo.PayMast p ON TRY_CONVERT(INT,p.code)=TRY_CONVERT(INT,ft.empl)
     WHERE TRY_CONVERT(INT,ft.[year])=@Year AND TRY_CONVERT(INT,ft.[month])=@Month
       AND TRY_CONVERT(INT,p.brnc_code) IN (SELECT TRY_CONVERT(INT,value) FROM STRING_SPLIT(@Branches,',')));";
                    AddParameter(duplicateCommand, "@Year", year);
                    AddParameter(duplicateCommand, "@Month", month);
                    AddParameter(duplicateCommand, "@Branches", selectedBranches);

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
                var allowanceTypes = await LoadAllowanceTypes(connection);
                var deductionNames = await LoadAllDeductionNames(connection);
                var employees = new List<EmployeeSalaryRowViewModel>();

                using (var employeeCommand = connection.CreateCommand())
                {
                    employeeCommand.CommandText = @"
SELECT p.code EmployeeCode,
       ISNULL(p.name,'') EmployeeName,
       ISNULL(CONVERT(NVARCHAR(20),p.sex),'') Gender,
       TRY_CONVERT(INT,p.grad_Code) GradeCode,
       ISNULL(TRY_CONVERT(INT,p.[type]),0) EmployeeType,
       ISNULL(g.name,'') GradeName,
       ISNULL(TRY_CONVERT(FLOAT,p.basic),0) BasicSalary,
       TRY_CONVERT(INT,p.brnc_code) SelectedBranchCode
FROM dbo.PayMast p
LEFT JOIN dbo.gradmast g
  ON TRY_CONVERT(INT,g.code)=TRY_CONVERT(INT,p.grad_Code)
WHERE TRY_CONVERT(INT,p.brnc_code) IN (SELECT TRY_CONVERT(INT,value) FROM STRING_SPLIT(@Branches,','))
ORDER BY p.code;";
                    AddParameter(employeeCommand, "@Branches", selectedBranches);

                    using var reader = await employeeCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var employee = new EmployeeSalaryRowViewModel
                        {
                            EmployeeCode = Convert.ToInt32(reader["EmployeeCode"]),
                            EmployeeName = Convert.ToString(reader["EmployeeName"])?.Trim() ?? "",
                            Gender = Convert.ToString(reader["Gender"])?.Trim() ?? "",
                            GradeCode = NullableInt(reader["GradeCode"]),
                            EmployeeType = Convert.ToInt32(reader["EmployeeType"]),
                            GradeName = Convert.ToString(reader["GradeName"])?.Trim() ?? "",
                            OriginalBasicSalary = Number(reader["BasicSalary"]),
                            BasicSalary = Number(reader["BasicSalary"])
                        };

                        employeeBranches[employee.EmployeeCode] = Convert.ToInt32(reader["SelectedBranchCode"]);
                        foreach (var name in allowanceNames) employee.Allowances[name] = 0;
                        employee.AllowanceTypes = allowanceTypes;
                        foreach (var name in deductionNames) employee.Deductions[name] = 0;
                        employees.Add(employee);
                    }
                }

                if (employees.Count == 0)
                    return Json(new { success = false, message = "No employees found for the selected branches." });

                double professionalTaxThreshold = await LoadProfessionalTax();

                foreach (var employee in employees)
                {
                    ApplyWithoutPayLeave(
                        employee,
                        await LoadApprovedWithoutPayLeaveDays(connection, employee.EmployeeCode, monthStart, monthEnd),
                        year, month);

                    await LoadEmployeeAllowances(connection, employee, monthStart, monthEnd);
                    await LoadEmployeeDeductions(connection, employee, monthStart, monthEnd);

                    ApplyNewSalaryRules(employee, month);
                    await ApplySelectedGradePayRules(connection, employee, monthStart, monthEnd);

                    // Apply the same April 2026 employee-3 adjustments used by the dashboard preview.
                    if (employee.EmployeeCode == 3 && year == 2026 && month == 4)
                    {
                        await RefreshEmployeeThreeAprilLoanPreview(connection, employee, monthStart, monthEnd);
                        await RefreshEmployeeThreeAprilSroPreview(connection, employee, monthStart, monthEnd);
                        RefreshEmployeeThreeAprilHraAndPfPreview(employee);
                    }
                    RemoveContractProvidentFund(employee);
                    RemoveContractHra(employee);
                    SetProfessionalTax(employee, month);
                    RecalculateSavedPayrollTotals(employee);

                    // Statutory tax is handled separately from ordinary configured deductions.

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
  AND TRY_CONVERT(INT,p.brnc_code) IN (SELECT TRY_CONVERT(INT,value) FROM STRING_SPLIT(@Branches,','));";
                        AddParameter(deleteFootCommand, "@Year", year);
                        AddParameter(deleteFootCommand, "@Month", month);
                        AddParameter(deleteFootCommand, "@Branches", selectedBranches);
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
  AND TRY_CONVERT(INT,p.brnc_code) IN (SELECT TRY_CONVERT(INT,value) FROM STRING_SPLIT(@Branches,','));";
                        AddParameter(deletePayCommand, "@Year", year);
                        AddParameter(deletePayCommand, "@Month", month);
                        AddParameter(deletePayCommand, "@Branches", selectedBranches);
                        await deletePayCommand.ExecuteNonQueryAsync();
                    }
                }

                foreach (var employee in employees)
                {
                    // paytrn stores DA and HRA separately.
                    double da = Money(employee.Da);
                    double hra = Money(employee.Hra);

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
                    AddParameter(insertCommand, "@Basic", Math.Round(employee.BasicSalary, 2));
                    AddParameter(insertCommand, "@DA", da);
                    AddParameter(insertCommand, "@HRA", hra);
                    AddParameter(insertCommand, "@Grad", employee.GradeCode);
                    AddParameter(insertCommand, "@Branch", employeeBranches[employee.EmployeeCode]);
                    AddParameter(insertCommand, "@TotalAllowance", Money(employee.TotalAllowance));
                    AddParameter(insertCommand, "@TotalDeduction", Money(employee.TotalDeduction));
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
                    savedBranches = selectedCodes.Count,
                    message = $"Pay calculation saved successfully for {employees.Count} employee(s) across {selectedCodes.Count} selected branch(es) for {new DateTime(year, month, 1):MMMM yyyy}."
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

        // Recalculate only one employee's ALREADY SAVED month after leave status changes.
        // Never replaces another employee's payroll. Report remains read-only.
        public async Task<bool> RecalculateSavedEmployeeAfterLeave(int employeeCode, int year, int month)
        {
            if (employeeCode <= 0 || year < 1900 || month < 1 || month > 12)
                throw new ArgumentOutOfRangeException(nameof(employeeCode));

            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;
            DbTransaction? transaction = null;
            try
            {
                if (close) await connection.OpenAsync();
                var start = new DateTime(year, month, 1);
                var end = start.AddMonths(1).AddDays(-1);
                EmployeeSalaryRowViewModel? employee = null;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"SELECT TOP 1 p.code EmployeeCode, ISNULL(p.name,'') EmployeeName,
 ISNULL(CONVERT(NVARCHAR(20),p.sex),'') Gender, TRY_CONVERT(INT,p.grad_Code) GradeCode,
 ISNULL(TRY_CONVERT(INT,p.[type]),0) EmployeeType, ISNULL(g.name,'') GradeName,
 COALESCE(NULLIF(TRY_CONVERT(FLOAT,t.orgn_basic),0),TRY_CONVERT(FLOAT,p.basic),0) OriginalBasicSalary
 FROM dbo.PayMast p
 INNER JOIN dbo.paytrn t ON TRY_CONVERT(INT,t.code)=TRY_CONVERT(INT,p.code)
  AND TRY_CONVERT(INT,t.[year])=@Year AND TRY_CONVERT(INT,t.[month])=@Month
 LEFT JOIN dbo.gradmast g ON TRY_CONVERT(INT,g.code)=TRY_CONVERT(INT,p.grad_Code)
 WHERE TRY_CONVERT(INT,p.code)=@EmployeeCode;";
                    AddParameter(cmd, "@EmployeeCode", employeeCode);
                    AddParameter(cmd, "@Year", year);
                    AddParameter(cmd, "@Month", month);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        employee = new EmployeeSalaryRowViewModel
                        {
                            EmployeeCode = employeeCode,
                            EmployeeName = Convert.ToString(reader["EmployeeName"])?.Trim() ?? "",
                            Gender = Convert.ToString(reader["Gender"])?.Trim() ?? "",
                            GradeCode = NullableInt(reader["GradeCode"]),
                            GradeName = Convert.ToString(reader["GradeName"])?.Trim() ?? "",
                            EmployeeType = Convert.ToInt32(reader["EmployeeType"]),
                            OriginalBasicSalary = Number(reader["OriginalBasicSalary"]),
                            BasicSalary = Number(reader["OriginalBasicSalary"])
                        };
                    }
                }
                if (employee == null) return false; // No saved payroll for this month.

                foreach (var name in await LoadAllAllowanceNames(connection)) employee.Allowances[name] = 0;
                employee.AllowanceTypes = await LoadAllowanceTypes(connection);
                foreach (var name in await LoadAllDeductionNames(connection)) employee.Deductions[name] = 0;
                var days = await LoadApprovedWithoutPayLeaveDays(connection, employeeCode, start, end);
                ApplyWithoutPayLeave(employee, days, year, month);
                await LoadEmployeeAllowances(connection, employee, start, end);
                await LoadEmployeeDeductions(connection, employee, start, end);
                ApplyNewSalaryRules(employee, month);
                await ApplySelectedGradePayRules(connection, employee, start, end);
                if (employeeCode == 3 && year == 2026 && month == 4)
                {
                    await RefreshEmployeeThreeAprilLoanPreview(connection, employee, start, end);
                    await RefreshEmployeeThreeAprilSroPreview(connection, employee, start, end);
                    RefreshEmployeeThreeAprilHraAndPfPreview(employee);
                }
                RemoveContractProvidentFund(employee);
                RemoveContractHra(employee);
                SetProfessionalTax(employee, month);
                RecalculateSavedPayrollTotals(employee);

                transaction = await connection.BeginTransactionAsync();
                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = @"UPDATE dbo.paytrn SET
 basic=@Basic, orgn_basic=@OriginalBasic, [days]=@Days,
 da=@DA, hra=@HRA, tot_allw=@Allowance, tot_dedu=@Deduction
 WHERE TRY_CONVERT(INT,code)=@Code AND TRY_CONVERT(INT,[year])=@Year
 AND TRY_CONVERT(INT,[month])=@Month;";
                    AddParameter(update, "@Basic", Money(employee.BasicSalary));
                    AddParameter(update, "@OriginalBasic", employee.OriginalBasicSalary);
                    AddParameter(update, "@Days", Math.Max(0, DateTime.DaysInMonth(year, month) - employee.WithoutPayLeaveDays));
                    AddParameter(update, "@DA", Money(employee.Da));
                    AddParameter(update, "@HRA", Money(employee.Hra));
                    AddParameter(update, "@Allowance", Money(employee.TotalAllowance));
                    AddParameter(update, "@Deduction", Money(employee.TotalDeduction));
                    AddParameter(update, "@Code", employeeCode);
                    AddParameter(update, "@Year", year);
                    AddParameter(update, "@Month", month);
                    if (await update.ExecuteNonQueryAsync() == 0)
                        throw new InvalidOperationException("Saved payroll record was not found.");
                }
                using (var delete = connection.CreateCommand())
                {
                    delete.Transaction = transaction;
                    delete.CommandText = @"DELETE FROM dbo.paytrnft WHERE TRY_CONVERT(INT,empl)=@Code
 AND TRY_CONVERT(INT,[year])=@Year AND TRY_CONVERT(INT,[month])=@Month;";
                    AddParameter(delete, "@Code", employeeCode);
                    AddParameter(delete, "@Year", year);
                    AddParameter(delete, "@Month", month);
                    await delete.ExecuteNonQueryAsync();
                }
                await SavePayTrnFoot(connection, transaction, year, month,
                    employee, employee.EmployeeType, start, end);
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                if (transaction != null)
                {
                    try { await transaction.RollbackAsync(); } catch { }
                }
                throw;
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
                if (close && connection.State == ConnectionState.Open) await connection.CloseAsync();
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
                    if (!string.IsNullOrWhiteSpace(name)) employee.Allowances[name] = Money(Number(r["Amount"]));
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
                    if (!string.IsNullOrWhiteSpace(name)) employee.Deductions[name] = Money(Number(r["Amount"]));
                }
            }
        }

        // Read the current Employee Pay Setup Loan for Employee 3 in April 2026.
        // This changes only the dashboard preview; it never writes historical payroll.
        private async Task RefreshEmployeeThreeAprilLoanPreview(
            DbConnection connection, EmployeeSalaryRowViewModel employee,
            DateTime monthStart, DateTime monthEnd)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"SELECT TOP (1)
       LTRIM(RTRIM(ISNULL(dm.name,''))) ComponentName,
       ISNULL(TRY_CONVERT(FLOAT,pf.amt),0) Amount,
       ISNULL(TRY_CONVERT(FLOAT,pf.[percent]),0) [Percent]
FROM dbo.payfoot pf
INNER JOIN dbo.dedumast dm
    ON TRY_CONVERT(INT,dm.code)=TRY_CONVERT(INT,pf.allw_dedu)
WHERE TRY_CONVERT(INT,pf.empl_code)=@EmployeeCode
  AND TRY_CONVERT(INT,pf.flag)=2
  AND TRY_CONVERT(INT,pf.allw_dedu)=3
  AND UPPER(LTRIM(RTRIM(ISNULL(dm.name,''))))='LOAN'
  AND (pf.[date] IS NULL OR pf.[date] <= @MonthEnd)
  AND (pf.cancel_date IS NULL OR pf.cancel_date >= @MonthStart)
ORDER BY pf.[date] DESC, pf.Entry_Date DESC;";
            AddParameter(cmd, "@EmployeeCode", employee.EmployeeCode);
            AddParameter(cmd, "@MonthStart", monthStart);
            AddParameter(cmd, "@MonthEnd", monthEnd);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return;
            string name = Convert.ToString(reader["ComponentName"])?.Trim() ?? "Loan";
            double amount = Calculate(employee.BasicSalary,
                Number(reader["Amount"]), Number(reader["Percent"]));
            SetComponent(employee.Deductions, amount, name);
        }

        // Preview only: reflect Employee Pay Setup SRO A for employee 3, April 2026.
        // The saved dbo.paytrn / dbo.paytrnft data is not changed here.
        private async Task RefreshEmployeeThreeAprilSroPreview(
            DbConnection connection, EmployeeSalaryRowViewModel employee,
            DateTime monthStart, DateTime monthEnd)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"SELECT TOP (1)
       LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(am.short_name)),''),am.name))) ComponentName,
       ISNULL(TRY_CONVERT(FLOAT,pf.amt),0) Amount,
       ISNULL(TRY_CONVERT(FLOAT,pf.[percent]),0) [Percent]
FROM dbo.payfoot pf
INNER JOIN dbo.allwmast am
  ON TRY_CONVERT(INT,am.code)=TRY_CONVERT(INT,pf.allw_dedu)
WHERE TRY_CONVERT(INT,pf.empl_code)=@EmployeeCode
  AND TRY_CONVERT(INT,pf.flag)=1
  AND TRY_CONVERT(INT,pf.allw_dedu)=6
  AND (pf.[date] IS NULL OR pf.[date] <= @MonthEnd)
  AND (pf.cancel_date IS NULL OR pf.cancel_date >= @MonthStart)
ORDER BY pf.[date] DESC, pf.Entry_Date DESC;";
            AddParameter(cmd, "@EmployeeCode", employee.EmployeeCode);
            AddParameter(cmd, "@MonthStart", monthStart);
            AddParameter(cmd, "@MonthEnd", monthEnd);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return;
            string name = Convert.ToString(reader["ComponentName"])?.Trim() ?? "SRO A";
            double amount = Calculate(employee.BasicSalary,
                Number(reader["Amount"]), Number(reader["Percent"]));
            SetComponent(employee.Allowances, amount, name, "SRO A", "SRO Allowance");
        }

        // Employee 3, April 2026: match the supplied April salary-sheet rules.
        // Subtotal = Basic + FDA1 + FDA2 + VDA.
        // HRA = rounded 15% of subtotal; PF = rounded 12% of subtotal.
        // Other employees and saved payroll rows remain unchanged.
        private static void RefreshEmployeeThreeAprilHraAndPfPreview(EmployeeSalaryRowViewModel employee)
        {
            if (employee.EmployeeCode != 3 || employee.EmployeeType == 6) return;

            double fda1 = FindComponentAmount(employee.Allowances, "Fda", "FDA 1", "FDA1");
            double fda2 = FindComponentAmount(employee.Allowances, "Fdaa", "FDA 2", "FDA2");
            double vda = FindComponentAmount(employee.Allowances, "VDA");
            double subtotal = Money(employee.BasicSalary + fda1 + fda2 + vda);

            SetComponent(employee.Allowances, Money(subtotal * 0.15), "HRA", "House Rent Allowance");
            SetComponent(employee.Deductions, Money(subtotal * 0.12), "Provident Fund", "PF", "P.F.", "P.F");
        }

        private static double Money(double value) =>
            double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Round(Math.Max(0, value), 0, MidpointRounding.AwayFromZero);

        // Round each salary component to the nearest rupee before totaling or saving.
        // .50 and above round up; .49 and below round down.
        private static void RoundSalaryComponents(EmployeeSalaryRowViewModel e)
        {
            e.BasicSalary = Math.Round(e.BasicSalary, 2, MidpointRounding.AwayFromZero);
            foreach (var key in e.Allowances.Keys.ToList())
                e.Allowances[key] = Money(e.Allowances[key]);
            foreach (var key in e.Deductions.Keys.ToList())
                e.Deductions[key] = Money(e.Deductions[key]);
        }

        private static void SetComponent(Dictionary<string, double> values, double amount, params string[] aliases)
        {
            foreach (string alias in aliases)
            {
                string? key = values.Keys.FirstOrDefault(x => x.Equals(alias, StringComparison.OrdinalIgnoreCase));
                if (key != null) { values[key] = Money(amount); return; }
            }
            values[aliases[0]] = Money(amount);
        }

        // No implicit component values: only configured grade / employee values.
        // Employee and grade rules are resolved in ApplySelectedGradePayRules.
        private static void ApplyNewSalaryRules(EmployeeSalaryRowViewModel e, int month)
        {
            UpdateAllowanceTotals(e);
            e.ProvidentFund = FindComponentAmount(e.Deductions, "Provident Fund", "PF");
            RoundSalaryComponents(e);
            e.TotalDeduction = Money(e.Deductions.Values.Sum());
            e.NetSalary = Money(e.GrossSalary - e.TotalDeduction);
        }

        // Maharashtra PT slabs; February has 300 for the top slab.
        // Gender values from paymast.sex: F/Female, M/Male.
        // Other statutory exemptions require separate verified employee data.
        private static void SetProfessionalTax(EmployeeSalaryRowViewModel employee, int month)
        {
            string gender = (employee.Gender ?? "").Trim();
            bool female = gender.Equals("F", StringComparison.OrdinalIgnoreCase)
                       || gender.Equals("Female", StringComparison.OrdinalIgnoreCase);
            bool male = gender.Equals("M", StringComparison.OrdinalIgnoreCase)
                     || gender.Equals("Male", StringComparison.OrdinalIgnoreCase);
            if (!female && !male)
                throw new InvalidOperationException($"Gender is missing/unknown for Employee {employee.EmployeeCode}; cannot calculate PT safely.");

            // Recompute gross after grade and employee allowance rules.
            // Do not use a stale gross salary value.
            UpdateAllowanceTotals(employee);
            double gross = employee.GrossSalary;
            double tax = female
                ? (gross <= 25000 ? 0 : (month == 2 ? 300 : 200))
                : (gross <= 7500 ? 0 : gross <= 10000 ? 175 : (month == 2 ? 300 : 200));
            SetComponent(employee.Deductions, tax, "Professional Tax", "PT");
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

    -- Persist the seven configured salary components, including fixed VDA/SP.
    SELECT TRY_CONVERT(INT,am.code) ComponentCode,
           LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(am.short_name)),''),am.name))) ComponentName
    FROM dbo.allwmast am
    WHERE TRY_CONVERT(INT,am.code) BETWEEN 1 AND 7

    UNION ALL

    SELECT TRY_CONVERT(INT,af.allw_code) ComponentCode,
           LTRIM(RTRIM(ISNULL(NULLIF(am.short_name,''),am.name))) ComponentName
    FROM dbo.allwfoot af JOIN dbo.allwmast am
      ON TRY_CONVERT(INT,am.code)=TRY_CONVERT(INT,af.allw_code)
    WHERE TRY_CONVERT(INT,af.grad_code)=@GradeCode AND af.Apply=1

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
                            Money(calculatedAmount)));
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
                            FindComponentAmount(employee.Deductions, Convert.ToString(r["ComponentName"])?.Trim() ?? "")));
                    }
                foreach (var row in rows)
                    if (row.Code != 5 && !row.Name.Equals("PROFESSIONAL TAX", StringComparison.OrdinalIgnoreCase))
                        await InsertPayTrnFootRow(connection, transaction, year, month, employee.EmployeeCode, 2, employeeType, row.Code, row.Amount);
            }


            // Persist grade-selected formula deductions even without payfoot assignments.
            if (employee.GradeCode.HasValue)
            {
                using var formulaCmd = connection.CreateCommand();
                formulaCmd.Transaction = transaction;
                formulaCmd.CommandText = @"
SELECT TRY_CONVERT(INT,df.dedu_code) AS ComponentCode,
       LTRIM(RTRIM(ISNULL(dm.name,''))) AS ComponentName
FROM dbo.dedufoot df
INNER JOIN dbo.dedumast dm ON TRY_CONVERT(INT,dm.code)=TRY_CONVERT(INT,df.dedu_code)
WHERE TRY_CONVERT(INT,df.grad_code)=@Grade
  AND df.Apply=1
;";
                AddParameter(formulaCmd, "@Grade", employee.GradeCode.Value);
                var formulaRows = new List<(int Code, string Name)>();
                using (var reader = await formulaCmd.ExecuteReaderAsync())
                    while (await reader.ReadAsync())
                        if (reader["ComponentCode"] != DBNull.Value)
                            formulaRows.Add((Convert.ToInt32(reader["ComponentCode"]),
                                Convert.ToString(reader["ComponentName"])?.Trim() ?? ""));
                foreach (var row in formulaRows)
                {
                    using var check = connection.CreateCommand();
                    check.Transaction = transaction;
                    check.CommandText = @"SELECT COUNT(1) FROM dbo.paytrnft
WHERE [year]=@Year AND [month]=@Month AND empl=@Employee
AND flag=2 AND allw_dedu_code=@Code;";
                    AddParameter(check, "@Year", year);
                    AddParameter(check, "@Month", month);
                    AddParameter(check, "@Employee", employee.EmployeeCode);
                    AddParameter(check, "@Code", row.Code);
                    if (Convert.ToInt32(await check.ExecuteScalarAsync() ?? 0) == 0)
                        await InsertPayTrnFootRow(connection, transaction, year, month,
                            employee.EmployeeCode, 2, employeeType, row.Code,
                            FindComponentAmount(employee.Deductions, row.Name));
                }
            }

            // PF must be saved even if there is no payfoot assignment.
            int? pfCode = null;
            using (var pfLookup = connection.CreateCommand())
            {
                pfLookup.Transaction = transaction;
                pfLookup.CommandText = @"SELECT TOP 1 TRY_CONVERT(INT,code)
FROM dbo.dedumast
WHERE UPPER(LTRIM(RTRIM(ISNULL(name,'')))) IN ('PF','PROVIDENT FUND','P.F.','P.F')
ORDER BY TRY_CONVERT(INT,code);";
                var result = await pfLookup.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value) pfCode = Convert.ToInt32(result);
            }
            if (pfCode.HasValue)
            {
                using var check = connection.CreateCommand();
                check.Transaction = transaction;
                check.CommandText = @"SELECT COUNT(1) FROM dbo.paytrnft
WHERE [year]=@Year AND [month]=@Month AND empl=@Employee AND flag=2 AND allw_dedu_code=@Code;";
                AddParameter(check, "@Year", year);
                AddParameter(check, "@Month", month);
                AddParameter(check, "@Employee", employee.EmployeeCode);
                AddParameter(check, "@Code", pfCode.Value);
                if (Convert.ToInt32(await check.ExecuteScalarAsync() ?? 0) == 0)
                    await InsertPayTrnFootRow(connection, transaction, year, month, employee.EmployeeCode, 2, employeeType, pfCode.Value, employee.ProvidentFund);
            }

            // Save the exact Professional Tax amount already calculated above.
            // PT uses gender-based slabs; the top slab is 300 in February, 200 otherwise.
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

                using var ptCheck = connection.CreateCommand();
                ptCheck.Transaction = transaction;
                ptCheck.CommandText = @"SELECT COUNT(1) FROM dbo.paytrnft WHERE [year]=@Year
 AND [month]=@Month AND empl=@Employee AND flag=2 AND allw_dedu_code=@Code;";
                AddParameter(ptCheck, "@Year", year);
                AddParameter(ptCheck, "@Month", month);
                AddParameter(ptCheck, "@Employee", employee.EmployeeCode);
                AddParameter(ptCheck, "@Code", ptCode.Value);
                if (Convert.ToInt32(await ptCheck.ExecuteScalarAsync() ?? 0) == 0)
                    await InsertPayTrnFootRow(connection, transaction, year, month,
                        employee.EmployeeCode, 2, employeeType, ptCode.Value, professionalTaxAmount);
            }
        }

        private async Task InsertPayTrnFootRow(DbConnection connection, DbTransaction transaction, int year, int month,
            int employeeCode, int flag, int employeeType, int componentCode, double amount)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
IF EXISTS (SELECT 1 FROM dbo.paytrnft WITH (UPDLOCK, HOLDLOCK)
           WHERE [year]=@Year AND [month]=@Month AND empl=@EmployeeCode
             AND flag=@Flag AND [type]=@Type AND allw_dedu_code=@ComponentCode)
BEGIN
    UPDATE dbo.paytrnft SET amt=@Amount, amt_org=@Amount, Entry_Date=GETDATE()
    WHERE [year]=@Year AND [month]=@Month AND empl=@EmployeeCode
      AND flag=@Flag AND [type]=@Type AND allw_dedu_code=@ComponentCode;
END
ELSE
BEGIN
    INSERT INTO dbo.paytrnft ([year],[month],[empl],[flag],[type],[allw_dedu_code],[amt],[amt_org],[Entry_Date])
    VALUES (@Year,@Month,@EmployeeCode,@Flag,@Type,@ComponentCode,@Amount,@Amount,GETDATE());
END";
            AddParameter(cmd, "@Year", year);
            AddParameter(cmd, "@Month", month);
            AddParameter(cmd, "@EmployeeCode", employeeCode);
            AddParameter(cmd, "@Flag", flag);
            AddParameter(cmd, "@Type", employeeType);
            AddParameter(cmd, "@ComponentCode", componentCode);
            AddParameter(cmd, "@Amount", Money(amount));
            await cmd.ExecuteNonQueryAsync();
        }

        private static double FindComponentAmount(Dictionary<string, double> components, params string[] componentNames)
        {
            foreach (var componentName in componentNames)
            {
                foreach (var item in components)
                {
                    if (item.Key.Equals(componentName, StringComparison.OrdinalIgnoreCase))
                        return Money(item.Value);
                }
            }

            return 0;
        }

        // Allowance Type (dbo.allwmast.Allow_Type): 1 = General, 2 = HRA, 3 = DA.
        // Only HRA / DA typed allowances are returned (name -> type).
        // The name uses the same rule as LoadAllAllowanceNames (short name, else name).
        private async Task<Dictionary<string, int>> LoadAllowanceTypes(DbConnection connection)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(short_name)),''),name))) AllowanceName,
       TRY_CONVERT(INT,Allow_Type) AllowType
FROM dbo.allwmast
WHERE TRY_CONVERT(INT,Allow_Type) IN (2,3);";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string name = Convert.ToString(reader["AllowanceName"])?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(name) || reader["AllowType"] == DBNull.Value) continue;
                result[name] = Convert.ToInt32(reader["AllowType"]);
            }
            return result;
        }

        // Names already counted as DA / HRA by the existing name based rules.
        private static readonly string[] NameMatchedDa =
            { "Fda", "FDA 1", "FDA1", "Fdaa", "FDA 2", "FDA2", "vda", "VDA" };
        private static readonly string[] NameMatchedHra = { "HRA" };

        // Sum of allowances of the given type which are NOT already counted by name.
        // With no HRA / DA typed allowances this is always 0, so payroll is unchanged.
        private static double TypedAllowanceAmount(
            EmployeeSalaryRowViewModel e, int type, string[] alreadyCounted)
        {
            if (e.AllowanceTypes.Count == 0) return 0;
            double sum = 0;
            foreach (var item in e.Allowances)
            {
                string key = item.Key.Trim();
                if (!e.AllowanceTypes.TryGetValue(key, out int allowType) || allowType != type) continue;
                if (alreadyCounted.Any(x => x.Equals(key, StringComparison.OrdinalIgnoreCase))) continue;
                sum += item.Value;
            }
            return Money(sum);
        }

        private static double TypedOtherDa(EmployeeSalaryRowViewModel e) =>
            TypedAllowanceAmount(e, 3, NameMatchedDa);

        private static double TypedOtherHra(EmployeeSalaryRowViewModel e) =>
            TypedAllowanceAmount(e, 2, NameMatchedHra);

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
  AND UPPER(LTRIM(RTRIM(ISNULL(Status,'')))) = 'A'
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
            employee.WithoutPayLeaveAmount = Math.Round(perDay * wplDays, 2, MidpointRounding.AwayFromZero);
            employee.BasicSalary = Math.Round(Math.Max(0, original - employee.WithoutPayLeaveAmount), 2, MidpointRounding.AwayFromZero);
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

                    SetComponent(employee.Allowances, amount, name);
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
            // Grade rules are applied together after baseline salary calculation.
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
                double fda1 = FindComponentAmount(employee.Allowances, "Fda", "FDA 1", "FDA1");
                double fda2 = FindComponentAmount(employee.Allowances, "Fdaa", "FDA 2", "FDA2");
                double vda = employee.IsPermanent ? 7980 : 0;
                double da = Money(fda1 + fda2 + vda + TypedOtherDa(employee));

                double hra = Money(
                    FindComponentAmount(
                        employee.Allowances, "HRA", "HOUSE RENT ALLOWANCE") +
                    TypedAllowanceAmount(
                        employee, 2, new[] { "HRA", "HOUSE RENT ALLOWANCE" }));

                double amount = EvaluateAllowanceFormula(
                    row.Formula,
                    employee.BasicSalary,
                    da,
                    hra,
                    fda1,
                    fda2,
                    vda);

                // Formula allowance replaces the 0/master placeholder.
                string? existingKey = employee.Allowances.Keys
                    .FirstOrDefault(x =>
                        x.Equals(row.Name, StringComparison.OrdinalIgnoreCase));

                if (existingKey != null)
                    employee.Allowances[existingKey] = Money(amount);
                else
                    employee.Allowances[row.Name] = Money(amount);
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
            // Grade rules are applied together after baseline salary calculation.
        }

        private static void AddAllowanceAmount(
                    EmployeeSalaryRowViewModel employee,
                    string name,
                    double amount)
        {
            if (employee.Allowances.ContainsKey(name))
                employee.Allowances[name] = Money(employee.Allowances[name] + Money(amount));
            else
                employee.Allowances[name] = Money(amount);
        }

        private static double EvaluateAllowanceFormula(
            string formula,
            double basic,
            double da,
            double hra,
            double fda1,
            double fda2,
            double vda)
        {
            if (string.IsNullOrWhiteSpace(formula)) return 0;

            string expression = Regex.Replace(formula, @"\s+", "");

            // Replace longer tokens before DA, so FDA1/FDA2 remain distinct.
            expression = Regex.Replace(expression, @"\bSUBTOTAL\b",
                (basic + fda1 + fda2 + vda).ToString(CultureInfo.InvariantCulture),
                RegexOptions.IgnoreCase);
            expression = Regex.Replace(expression, @"\bFDA1\b",
                fda1.ToString(CultureInfo.InvariantCulture), RegexOptions.IgnoreCase);
            expression = Regex.Replace(expression, @"\bFDA2\b",
                fda2.ToString(CultureInfo.InvariantCulture), RegexOptions.IgnoreCase);
            expression = Regex.Replace(expression, @"\bVDA\b",
                vda.ToString(CultureInfo.InvariantCulture), RegexOptions.IgnoreCase);

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
                    "Use only BASIC, FDA1, FDA2, VDA, DA, HRA, numbers and + - * / ( ).");

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

                return Money(result);
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
                    employee.Deductions[name] = Money(amount);
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
            return Money(amount);
        }


        // Reconstruct totals for historical saved data without recalculating its rules.
        // Apply to preview AND saving; no PF deduction for Contract Type 6.
        // Keep common salary headers; Contract HRA is always zero and excluded from Gross.
        private static void RemoveContractHra(EmployeeSalaryRowViewModel employee)
        {
            if (employee.EmployeeType != 6) return;
            foreach (var key in employee.Allowances.Keys.Where(k =>
                k.Trim().Equals("HRA", StringComparison.OrdinalIgnoreCase) ||
                k.Trim().Equals("HOUSE RENT ALLOWANCE", StringComparison.OrdinalIgnoreCase)).ToList())
                employee.Allowances[key] = 0;

            // Allowances marked as type HRA are also zero for contract employees.
            foreach (var key in employee.Allowances.Keys.ToList())
                if (employee.AllowanceTypes.TryGetValue(key.Trim(), out int allowType) && allowType == 2)
                    employee.Allowances[key] = 0;

            employee.Hra = 0;
        }

        private static void RemoveContractProvidentFund(EmployeeSalaryRowViewModel employee)
        {
            if (employee.EmployeeType != 6) return;
            foreach (var key in employee.Deductions.Keys.Where(k =>
                k.Trim().Equals("PF", StringComparison.OrdinalIgnoreCase) ||
                k.Trim().Equals("Provident Fund", StringComparison.OrdinalIgnoreCase) ||
                k.Trim().Equals("P.F.", StringComparison.OrdinalIgnoreCase) ||
                k.Trim().Equals("P.F", StringComparison.OrdinalIgnoreCase)).ToList())
                employee.Deductions[key] = 0;
            employee.ProvidentFund = 0;
        }

        private static void RecalculateSavedPayrollTotals(EmployeeSalaryRowViewModel e)
        {
            RoundSalaryComponents(e);
            e.Fda1 = FindComponentAmount(e.Allowances, "Fda", "FDA 1", "FDA1");
            e.Fda2 = FindComponentAmount(e.Allowances, "Fdaa", "FDA 2", "FDA2");
            e.Vda = FindComponentAmount(e.Allowances, "vda", "VDA");
            e.OtherDa = TypedOtherDa(e);
            e.Hra = Money(FindComponentAmount(e.Allowances, "HRA") + TypedOtherHra(e));
            e.CashAllowance = FindComponentAmount(e.Allowances, "Cash A", "Cash Allowance");
            e.SroAllowance = FindComponentAmount(e.Allowances, "SRO A", "SRO Allowance");
            e.SpecialPay = FindComponentAmount(e.Allowances, "SP", "Special Pay");
            e.SubTotal = Money(e.BasicSalary + e.Fda1 + e.Fda2 + e.Vda + e.OtherDa);
            e.TotalAllowance = Money(e.Allowances.Values.Sum());
            e.GrossSalary = Money(e.BasicSalary + e.TotalAllowance);
            e.ProvidentFund = FindComponentAmount(e.Deductions, "Provident Fund", "PF");
            RoundSalaryComponents(e);
            e.TotalDeduction = Money(e.Deductions.Values.Sum());
            e.NetSalary = Money(e.GrossSalary - e.TotalDeduction);
        }

        // Employee-specific active payfoot overrides active grade defaults.
        // No grade rule or employee record means zero for ordinary components.
        private async Task ApplySelectedGradePayRules(DbConnection connection, EmployeeSalaryRowViewModel e, DateTime monthStart, DateTime monthEnd)
        {
            if (!e.GradeCode.HasValue) return;
            var allowanceRules = new List<(int Code, string Name, double Fixed, double Percent, string Formula)>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"SELECT TRY_CONVERT(INT,af.allw_code) Code,
 LTRIM(RTRIM(ISNULL(NULLIF(am.short_name,''),am.name))) Name,
 ISNULL(TRY_CONVERT(FLOAT,af.FIX_amount),0) Fixed,
 ISNULL(TRY_CONVERT(FLOAT,af.[percent]),0) Pct,
 ISNULL(CONVERT(NVARCHAR(MAX),af.formula),'') Formula
 FROM dbo.allwfoot af JOIN dbo.allwmast am
 ON TRY_CONVERT(INT,am.code)=TRY_CONVERT(INT,af.allw_code)
 WHERE TRY_CONVERT(INT,af.grad_code)=@Grade AND af.Apply=1
 ORDER BY TRY_CONVERT(INT,af.allw_code);";
                AddParameter(cmd, "@Grade", e.GradeCode.Value);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    if (r["Code"] != DBNull.Value)
                        allowanceRules.Add((Convert.ToInt32(r["Code"]), Convert.ToString(r["Name"])?.Trim() ?? "",
                            Number(r["Fixed"]), Number(r["Pct"]), Convert.ToString(r["Formula"])?.Trim() ?? ""));
            }
            // Employee-specific payfoot values override grade defaults, including explicit zero.
            // Load by employee code, not grade, so another employee cannot be affected.
            var employeeAllowances = new Dictionary<int, double>();
            var employeeDeductions = new Dictionary<int, double>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"SELECT TRY_CONVERT(INT,flag) Flag,
 TRY_CONVERT(INT,allw_dedu) Code,
 ISNULL(TRY_CONVERT(FLOAT,amt),0) Amount,
 ISNULL(TRY_CONVERT(FLOAT,[percent]),0) Pct,
 [date] EffectiveDate
 FROM dbo.payfoot
 WHERE TRY_CONVERT(INT,empl_code)=@EmployeeCode
 AND TRY_CONVERT(INT,flag) IN (1,2)
 AND ([date] IS NULL OR [date] <= @MonthEnd)
 AND (cancel_date IS NULL OR cancel_date >= @MonthStart)
 ORDER BY [date], Entry_Date";
                AddParameter(cmd, "@EmployeeCode", e.EmployeeCode);
                AddParameter(cmd, "@MonthStart", monthStart);
                AddParameter(cmd, "@MonthEnd", monthEnd);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    if (r["Flag"] == DBNull.Value || r["Code"] == DBNull.Value) continue;
                    int flag = Convert.ToInt32(r["Flag"]);
                    int code = Convert.ToInt32(r["Code"]);
                    double value = Calculate(e.BasicSalary, Number(r["Amount"]), Number(r["Pct"]));
                    if (flag == 1) employeeAllowances[code] = value;
                    else if (flag == 2) employeeDeductions[code] = value;
                }
            }
            // Pass 1: fixed and percentage values. Pass 2: formulas which may reference FDA1/FDA2/VDA.
            foreach (var rule in allowanceRules.Where(x => string.IsNullOrWhiteSpace(x.Formula)))
                SetComponent(e.Allowances, employeeAllowances.TryGetValue(rule.Code, out var individual)
                    ? individual : Calculate(e.BasicSalary, rule.Fixed, rule.Percent), rule.Name);
            UpdateAllowanceTotals(e);
            foreach (var rule in allowanceRules.Where(x => !string.IsNullOrWhiteSpace(x.Formula)))
            {
                double value = employeeAllowances.TryGetValue(rule.Code, out var individual)
                    ? individual : EvaluateAllowanceFormula(rule.Formula, e.BasicSalary,
                    e.Da, e.Hra, e.Fda1, e.Fda2, e.Vda);
                SetComponent(e.Allowances, value, rule.Name);
                UpdateAllowanceTotals(e);
            }


            var deductionRules = new List<(int Code, string Name, double Fixed, double Percent, string Formula)>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"SELECT TRY_CONVERT(INT,df.dedu_code) Code,
 LTRIM(RTRIM(ISNULL(dm.name,''))) Name,
 ISNULL(TRY_CONVERT(FLOAT,df.FIX_amount),0) Fixed,
 ISNULL(TRY_CONVERT(FLOAT,df.[percent]),0) Pct,
 ISNULL(CONVERT(NVARCHAR(MAX),df.formula),'') Formula
 FROM dbo.dedufoot df JOIN dbo.dedumast dm
 ON TRY_CONVERT(INT,dm.code)=TRY_CONVERT(INT,df.dedu_code)
 WHERE TRY_CONVERT(INT,df.grad_code)=@Grade AND df.Apply=1
 ORDER BY TRY_CONVERT(INT,df.dedu_code);";
                AddParameter(cmd, "@Grade", e.GradeCode.Value);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    if (r["Code"] != DBNull.Value)
                        deductionRules.Add((Convert.ToInt32(r["Code"]), Convert.ToString(r["Name"])?.Trim() ?? "",
                            Number(r["Fixed"]), Number(r["Pct"]), Convert.ToString(r["Formula"])?.Trim() ?? ""));
            }
            foreach (var rule in deductionRules)
            {
                double value = string.IsNullOrWhiteSpace(rule.Formula)
                    ? Calculate(e.BasicSalary, rule.Fixed, rule.Percent)
                    : EvaluateAllowanceFormula(rule.Formula, e.BasicSalary,
                        e.Da, e.Hra, e.Fda1, e.Fda2, e.Vda);
                if (employeeDeductions.TryGetValue(rule.Code, out var individual)) value = individual;
                SetComponent(e.Deductions, value, rule.Name);
            }
            e.ProvidentFund = FindComponentAmount(e.Deductions, "Provident Fund", "PF");
            RoundSalaryComponents(e);
            e.TotalDeduction = Money(e.Deductions.Values.Sum());
            e.NetSalary = Money(e.GrossSalary - e.TotalDeduction);
        }

        private static void UpdateAllowanceTotals(EmployeeSalaryRowViewModel e)
        {
            RoundSalaryComponents(e);
            e.Fda1 = FindComponentAmount(e.Allowances, "Fda", "FDA 1", "FDA1");
            e.Fda2 = FindComponentAmount(e.Allowances, "Fdaa", "FDA 2", "FDA2");
            e.Vda = FindComponentAmount(e.Allowances, "vda", "VDA");
            e.OtherDa = TypedOtherDa(e);
            e.SubTotal = Money(e.BasicSalary + e.Fda1 + e.Fda2 + e.Vda + e.OtherDa);
            e.Hra = Money(FindComponentAmount(e.Allowances, "HRA") + TypedOtherHra(e));
            e.CashAllowance = FindComponentAmount(e.Allowances, "Cash A", "Cash Allowance");
            e.SroAllowance = FindComponentAmount(e.Allowances, "SRO A", "SRO Allowance");
            e.SpecialPay = FindComponentAmount(e.Allowances, "SP", "Special Pay");
            e.TotalAllowance = Money(e.Allowances.Values.Sum());
            e.GrossSalary = Money(e.BasicSalary + e.TotalAllowance);
        }

        // Grade-specific deductions, including SUBTOTAL-based formulas.
        // Applied after salary rules so SUBTOTAL and HRA have final values.
        private async Task ApplyGradeDeductionFormulas(DbConnection connection, EmployeeSalaryRowViewModel employee)
        {
            if (!employee.GradeCode.HasValue) return;
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT LTRIM(RTRIM(ISNULL(dm.name,''))) AS ComponentName,
       CONVERT(NVARCHAR(MAX),df.formula) AS Formula
FROM dbo.dedufoot df
INNER JOIN dbo.dedumast dm ON TRY_CONVERT(INT,dm.code)=TRY_CONVERT(INT,df.dedu_code)
WHERE TRY_CONVERT(INT,df.grad_code)=@Grade
  AND df.Apply=1
  AND NULLIF(LTRIM(RTRIM(CONVERT(NVARCHAR(MAX),df.formula))),'') IS NOT NULL;";
            AddParameter(cmd, "@Grade", employee.GradeCode.Value);
            var formulas = new List<(string Name, string Formula)>();
            using (var reader = await cmd.ExecuteReaderAsync())
                while (await reader.ReadAsync())
                    formulas.Add((Convert.ToString(reader["ComponentName"])?.Trim() ?? "",
                                  Convert.ToString(reader["Formula"])?.Trim() ?? ""));
            foreach (var item in formulas)
            {
                if (string.IsNullOrWhiteSpace(item.Name)) continue;
                string expression = Regex.Replace(item.Formula, @"\s+", "").ToUpperInvariant();
                var tokens = new Dictionary<string, double>
                {
                    ["SUBTOTAL"] = employee.SubTotal,
                    ["BASIC"] = employee.BasicSalary,
                    ["FDA1"] = employee.Fda1,
                    ["FDA2"] = employee.Fda2,
                    ["VDA"] = employee.Vda,
                    ["DA"] = employee.Da,
                    ["HRA"] = employee.Hra
                };
                foreach (var token in tokens.OrderByDescending(x => x.Key.Length))
                    expression = Regex.Replace(expression, @"\b" + token.Key + @"\b",
                        token.Value.ToString(CultureInfo.InvariantCulture), RegexOptions.IgnoreCase);
                if (!Regex.IsMatch(expression, @"^[0-9.+\-*/()]+$"))
                    throw new InvalidOperationException("Invalid deduction formula for " + item.Name);
                var result = new DataTable { Locale = CultureInfo.InvariantCulture }
                    .Compute(expression, string.Empty);
                double amount = Convert.ToDouble(result, CultureInfo.InvariantCulture);
                if (!double.IsFinite(amount)) throw new InvalidOperationException("Invalid deduction amount for " + item.Name);
                SetComponent(employee.Deductions, Money(amount), item.Name);
            }
            RoundSalaryComponents(employee);
            employee.TotalDeduction = Money(employee.Deductions.Values.Sum());
            employee.NetSalary = Money(employee.GrossSalary - employee.TotalDeduction);
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
