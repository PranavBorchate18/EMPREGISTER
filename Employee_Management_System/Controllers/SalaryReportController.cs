using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace Employee_Management_System.Controllers
{
    public class SalaryReportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SalaryReportController(ApplicationDbContext context)
        {
            _context = context;
        }

        // READ-ONLY PAGE
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            int currentYear = DateTime.Now.Year;
            int currentMonth = DateTime.Now.Month;

            var branches = await LoadBranches(
                currentYear, currentMonth,
                currentYear, currentMonth);

            return View(new SalaryReportViewModel
            {
                FromYear = currentYear,
                FromMonth = currentMonth,
                ToYear = currentYear,
                ToMonth = currentMonth,
                Branches = branches,
                AllowanceOptions = await LoadComponentOptions(true),
                DeductionOptions = await LoadComponentOptions(false),

                // All Branches Total
                AllBranchesTotalEmployees = branches.Sum(x => x.EmployeeCount),
                AllBranchesTotalBasic = Math.Round(branches.Sum(x => x.TotalBasic), 2),
                AllBranchesTotalAllowance = Math.Round(branches.Sum(x => x.TotalAllowance), 2),
                AllBranchesTotalDeduction = Math.Round(branches.Sum(x => x.TotalDeduction), 2),
                AllBranchesTotalNetSalary = Math.Round(branches.Sum(x => x.TotalNetSalary), 2)
            });
        }

        // IMPORTANT:
        // Branch master comes from dbo.brncmast.
        // EmployeeCount is ONLY from dbo.paytrn for the selected From/To period.
        // PayMast is used to map saved payroll employees to their branch.
        private async Task<List<SalaryReportBranchViewModel>> LoadBranches(int fromYear, int fromMonth, int toYear, int toMonth)
        {
            var result = new List<SalaryReportBranchViewModel>();
            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;

            try
            {
                if (close) await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
SELECT TRY_CONVERT(INT,b.code) BranchCode,
       ISNULL(b.name,'') BranchName,
       COUNT(DISTINCT CASE
           WHEN t.code IS NOT NULL THEN TRY_CONVERT(INT,t.code)
       END) EmployeeCount,
       ISNULL(SUM(CASE WHEN t.code IS NOT NULL THEN ISNULL(TRY_CONVERT(FLOAT,t.basic),0) ELSE 0 END),0) TotalBasic,
       ISNULL(SUM(CASE WHEN t.code IS NOT NULL THEN ISNULL(TRY_CONVERT(FLOAT,t.tot_allw),0) ELSE 0 END),0) TotalAllowance,
       ISNULL(SUM(CASE WHEN t.code IS NOT NULL THEN ISNULL(TRY_CONVERT(FLOAT,t.tot_dedu),0) ELSE 0 END),0) TotalDeduction,
       ISNULL(SUM(CASE WHEN t.code IS NOT NULL THEN
           ISNULL(TRY_CONVERT(FLOAT,t.basic),0) +
           ISNULL(TRY_CONVERT(FLOAT,t.tot_allw),0) -
           ISNULL(TRY_CONVERT(FLOAT,t.tot_dedu),0) ELSE 0 END),0) TotalNetSalary
FROM dbo.brncmast b
LEFT JOIN dbo.PayMast p
  ON TRY_CONVERT(INT,p.brnc_code)=TRY_CONVERT(INT,b.code)
LEFT JOIN dbo.paytrn t
  ON TRY_CONVERT(INT,t.code)=TRY_CONVERT(INT,p.code)
 AND ((TRY_CONVERT(INT,t.[year]) * 12 + TRY_CONVERT(INT,t.[month]))
      BETWEEN (@FromYear * 12 + @FromMonth) AND (@ToYear * 12 + @ToMonth))
GROUP BY b.code,b.name
ORDER BY TRY_CONVERT(INT,b.code);";

                AddParameter(command, "@FromYear", fromYear);
                AddParameter(command, "@FromMonth", fromMonth);
                AddParameter(command, "@ToYear", toYear);
                AddParameter(command, "@ToMonth", toMonth);

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (reader["BranchCode"] == DBNull.Value) continue;

                    result.Add(new SalaryReportBranchViewModel
                    {
                        BranchCode = Convert.ToInt32(reader["BranchCode"]),
                        BranchName = Convert.ToString(reader["BranchName"])?.Trim() ?? "",
                        EmployeeCount = Convert.ToInt32(reader["EmployeeCount"]),
                        TotalBasic = Number(reader["TotalBasic"]),
                        TotalAllowance = Number(reader["TotalAllowance"]),
                        TotalDeduction = Number(reader["TotalDeduction"]),
                        TotalNetSalary = Number(reader["TotalNetSalary"])
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

        // READ ONLY: Refresh branch totals for selected From/To period.
        [HttpGet]
        public async Task<IActionResult> GetBranchSummaries(
            int fromYear, int fromMonth, int toYear, int toMonth)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, sessionExpired = true, message = "Login session expired." });

            if (fromYear < 1900 || toYear < 1900 || fromMonth < 1 || fromMonth > 12 ||
                toMonth < 1 || toMonth > 12)
                return Json(new { success = false, message = "Please select a valid From/To period." });

            var from = new DateTime(fromYear, fromMonth, 1);
            var to = new DateTime(toYear, toMonth, 1);
            if (from > to)
                return Json(new { success = false, message = "From Month cannot be after To Month." });

            try
            {
                var branches = await LoadBranches(fromYear, fromMonth, toYear, toMonth);

                var allBranchesTotal = new
                {
                    totalEmployees = branches.Sum(x => x.EmployeeCount),
                    totalBasic = Math.Round(branches.Sum(x => x.TotalBasic), 2),
                    totalAllowance = Math.Round(branches.Sum(x => x.TotalAllowance), 2),
                    totalDeduction = Math.Round(branches.Sum(x => x.TotalDeduction), 2),
                    totalNetSalary = Math.Round(branches.Sum(x => x.TotalNetSalary), 2)
                };

                return Json(new
                {
                    success = true,
                    data = branches,
                    allBranchesTotal
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Unable to load branch summary. " + ex.Message });
            }
        }

        // READ ONLY:
        // Same calculation/fetch logic as Pay Calculation.
        // No INSERT / UPDATE / DELETE / SAVE.
        [HttpGet]
        public async Task<IActionResult> GetSalaryReport(int year, int month, int branchCode)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, sessionExpired = true, message = "Login session expired." });

            if (year < 1900 || month < 1 || month > 12 || branchCode <= 0)
                return Json(new { success = false, message = "Please select valid Year, Month and Branch." });

            DateTime monthStart = new DateTime(year, month, 1);
            DateTime monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var employees = new List<SalaryReportEmployeeRowViewModel>();
            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;

            try
            {
                if (close) await connection.OpenAsync();

                // Same as Pay Calculation: headings come from master tables.
                var allowanceNames = await LoadAllAllowanceNames(connection);

                // All deductions are shown, including Professional Tax.
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
                        var employee = new SalaryReportEmployeeRowViewModel
                        {
                            EmployeeCode = Convert.ToInt32(reader["EmployeeCode"]),
                            EmployeeName = Convert.ToString(reader["EmployeeName"])?.Trim() ?? "",
                            GradeCode = NullableInt(reader["GradeCode"]),
                            GradeName = Convert.ToString(reader["GradeName"])?.Trim() ?? "",
                            BasicSalary = Number(reader["BasicSalary"])
                        };

                        foreach (var name in allowanceNames)
                            employee.Allowances[name] = 0;

                        foreach (var name in deductionNames)
                            employee.Deductions[name] = 0;

                        employees.Add(employee);
                    }
                }

                double professionalTaxThreshold = await LoadProfessionalTax(connection);

                foreach (var employee in employees)
                {
                    await LoadEmployeeAllowances(connection, employee, monthStart, monthEnd);
                    await LoadEmployeeDeductions(connection, employee, monthStart, monthEnd);

                    employee.TotalAllowance =
                        Math.Round(employee.Allowances.Values.Sum(), 2);

                    employee.GrossSalary =
                        Math.Round(employee.BasicSalary + employee.TotalAllowance, 2);

                    // Same Professional Tax rule as Pay Calculation:
                    // Gross below threshold = 0
                    // Gross >= threshold: March = 300, April-February = 200
                    SetProfessionalTax(employee, professionalTaxThreshold, month);

                    employee.TotalDeduction =
                        Math.Round(employee.Deductions.Values.Sum(), 2);

                    employee.NetSalary =
                        Math.Round(employee.GrossSalary - employee.TotalDeduction, 2);
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
                return Json(new
                {
                    success = false,
                    message = "Unable to load Salary Report. " + ex.Message
                });
            }
            finally
            {
                if (close && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }

        // READ ONLY: Multi-month report.
        // IMPORTANT: Allowance/Deduction history is fetched ONLY from dbo.paytrnft
        // for the exact Employee + Year + Month. Missing component = 0.00.
        [HttpGet]
        public async Task<IActionResult> GetSalaryReportRange(
            int fromYear, int fromMonth, int toYear, int toMonth, int branchCode)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, sessionExpired = true, message = "Login session expired." });

            if (fromYear < 1900 || toYear < 1900 || fromMonth < 1 || fromMonth > 12 ||
                toMonth < 1 || toMonth > 12 || branchCode <= 0)
                return Json(new { success = false, message = "Please select a valid From/To period and Branch." });

            var from = new DateTime(fromYear, fromMonth, 1);
            var to = new DateTime(toYear, toMonth, 1);

            if (from > to)
                return Json(new { success = false, message = "From Month cannot be after To Month." });

            int monthCount = ((to.Year - from.Year) * 12) + to.Month - from.Month + 1;
            if (monthCount > 60)
                return Json(new { success = false, message = "Please select a range of 60 months or less." });

            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;

            try
            {
                if (close) await connection.OpenAsync();

                // Headings are common for all employees/months.
                var allowanceNames = await LoadAllAllowanceNames(connection);
                var deductionNames = await LoadAllDeductionNames(connection);
                var employees = new List<SalaryReportEmployeeRowViewModel>();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT p.code EmployeeCode,
       ISNULL(p.name,'') EmployeeName,
       TRY_CONVERT(INT,p.grad_Code) GradeCode,
       ISNULL(g.name,'') GradeName
FROM dbo.PayMast p
LEFT JOIN dbo.gradmast g
  ON TRY_CONVERT(INT,g.code)=TRY_CONVERT(INT,p.grad_Code)
WHERE TRY_CONVERT(INT,p.brnc_code)=@BranchCode
ORDER BY p.code;";

                    AddParameter(command, "@BranchCode", branchCode);

                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var employee = new SalaryReportEmployeeRowViewModel
                        {
                            EmployeeCode = Convert.ToInt32(reader["EmployeeCode"]),
                            EmployeeName = Convert.ToString(reader["EmployeeName"])?.Trim() ?? "",
                            GradeCode = NullableInt(reader["GradeCode"]),
                            GradeName = Convert.ToString(reader["GradeName"])?.Trim() ?? ""
                        };

                        foreach (var name in allowanceNames)
                            employee.Allowances[name] = 0;

                        foreach (var name in deductionNames)
                            employee.Deductions[name] = 0;

                        employees.Add(employee);
                    }
                }

                foreach (var employee in employees)
                {
                    for (var cursor = from; cursor <= to; cursor = cursor.AddMonths(1))
                    {
                        // Every selected month starts with ZERO.
                        // No previous/current month value is carried forward.
                        var history = new SalaryReportMonthHistoryViewModel
                        {
                            Year = cursor.Year,
                            Month = cursor.Month,
                            MonthName = cursor.ToString("MMMM")
                        };

                        foreach (var name in allowanceNames)
                            history.Allowances[name] = 0;

                        foreach (var name in deductionNames)
                            history.Deductions[name] = 0;

                        // Saved Basic for this exact Employee + Year + Month.
                        history.BasicSalary = await LoadSavedBasic(
                            connection, employee.EmployeeCode, cursor.Year, cursor.Month);

                        // Saved component values ONLY for this exact Employee + Year + Month.
                        await LoadSavedPayTrnFtAllowances(
                            connection, history, employee.EmployeeCode, cursor.Year, cursor.Month);

                        await LoadSavedPayTrnFtDeductions(
                            connection, history, employee.EmployeeCode, cursor.Year, cursor.Month);

                        history.TotalAllowance =
                            Math.Round(history.Allowances.Values.Sum(), 2);

                        history.GrossSalary =
                            Math.Round(history.BasicSalary + history.TotalAllowance, 2);

                        history.TotalDeduction =
                            Math.Round(history.Deductions.Values.Sum(), 2);

                        history.NetSalary =
                            Math.Round(history.GrossSalary - history.TotalDeduction, 2);

                        employee.History.Add(history);

                        // Main row = total summary of ONLY the selected period.
                        employee.BasicSalary += history.BasicSalary;
                        employee.TotalAllowance += history.TotalAllowance;
                        employee.GrossSalary += history.GrossSalary;
                        employee.TotalDeduction += history.TotalDeduction;
                        employee.NetSalary += history.NetSalary;

                        foreach (var name in allowanceNames)
                            employee.Allowances[name] += DictionaryAmount(history.Allowances, name);

                        foreach (var name in deductionNames)
                            employee.Deductions[name] += DictionaryAmount(history.Deductions, name);
                    }

                    employee.BasicSalary = Math.Round(employee.BasicSalary, 2);
                    employee.TotalAllowance = Math.Round(employee.TotalAllowance, 2);
                    employee.GrossSalary = Math.Round(employee.GrossSalary, 2);
                    employee.TotalDeduction = Math.Round(employee.TotalDeduction, 2);
                    employee.NetSalary = Math.Round(employee.NetSalary, 2);

                    foreach (var name in allowanceNames)
                        employee.Allowances[name] = Math.Round(employee.Allowances[name], 2);

                    foreach (var name in deductionNames)
                        employee.Deductions[name] = Math.Round(employee.Deductions[name], 2);
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
                return Json(new
                {
                    success = false,
                    message = "Unable to load Salary Report. " + ex.Message
                });
            }
            finally
            {
                if (close && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }


        // ============================================================
        // READ ONLY: Printable branch salary report
        // Uses SAVED payroll data from dbo.paytrn / dbo.paytrnft.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Print(
            int fromYear, int fromMonth,
            int toYear, int toMonth,
            int branchCode,
            List<string>? hiddenAllowanceNames = null,
            List<string>? hiddenDeductionNames = null)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            if (fromYear < 1900 || toYear < 1900 ||
                fromMonth < 1 || fromMonth > 12 ||
                toMonth < 1 || toMonth > 12 ||
                branchCode <= 0)
            {
                return BadRequest("Please select a valid From/To period and Branch.");
            }

            var from = new DateTime(fromYear, fromMonth, 1);
            var to = new DateTime(toYear, toMonth, 1);

            if (from > to)
                return BadRequest("From Month cannot be after To Month.");

            int monthCount = ((to.Year - from.Year) * 12) + to.Month - from.Month + 1;
            if (monthCount > 60)
                return BadRequest("Please select a range of 60 months or less.");

            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;

            try
            {
                if (close) await connection.OpenAsync();

                string branchName = "";
                using (var branchCommand = connection.CreateCommand())
                {
                    branchCommand.CommandText = @"
SELECT TOP 1 ISNULL(name,'')
FROM dbo.brncmast
WHERE TRY_CONVERT(INT,code)=@BranchCode;";
                    AddParameter(branchCommand, "@BranchCode", branchCode);

                    var branchValue = await branchCommand.ExecuteScalarAsync();
                    branchName = branchValue == null || branchValue == DBNull.Value
                        ? ""
                        : Convert.ToString(branchValue)?.Trim() ?? "";
                }

                var allowanceNames = await LoadAllAllowanceNames(connection);
                var deductionNames = await LoadAllDeductionNames(connection);
                var employees = new List<SalaryReportEmployeeRowViewModel>();

                // Only employees having SAVED payroll in dbo.paytrn
                // for the selected period are included in the print report.
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT DISTINCT
       p.code EmployeeCode,
       ISNULL(p.name,'') EmployeeName,
       TRY_CONVERT(INT,p.grad_Code) GradeCode,
       ISNULL(g.name,'') GradeName
FROM dbo.PayMast p
LEFT JOIN dbo.gradmast g
  ON TRY_CONVERT(INT,g.code)=TRY_CONVERT(INT,p.grad_Code)
INNER JOIN dbo.paytrn t
  ON TRY_CONVERT(INT,t.code)=TRY_CONVERT(INT,p.code)
 AND ((TRY_CONVERT(INT,t.[year]) * 12 + TRY_CONVERT(INT,t.[month]))
      BETWEEN (@FromYear * 12 + @FromMonth)
          AND (@ToYear * 12 + @ToMonth))
WHERE TRY_CONVERT(INT,p.brnc_code)=@BranchCode
ORDER BY p.code;";

                    AddParameter(command, "@FromYear", fromYear);
                    AddParameter(command, "@FromMonth", fromMonth);
                    AddParameter(command, "@ToYear", toYear);
                    AddParameter(command, "@ToMonth", toMonth);
                    AddParameter(command, "@BranchCode", branchCode);

                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var employee = new SalaryReportEmployeeRowViewModel
                        {
                            EmployeeCode = Convert.ToInt32(reader["EmployeeCode"]),
                            EmployeeName = Convert.ToString(reader["EmployeeName"])?.Trim() ?? "",
                            GradeCode = NullableInt(reader["GradeCode"]),
                            GradeName = Convert.ToString(reader["GradeName"])?.Trim() ?? ""
                        };

                        foreach (var name in allowanceNames)
                            employee.Allowances[name] = 0;

                        foreach (var name in deductionNames)
                            employee.Deductions[name] = 0;

                        employees.Add(employee);
                    }
                }

                foreach (var employee in employees)
                {
                    for (var cursor = from; cursor <= to; cursor = cursor.AddMonths(1))
                    {
                        var history = new SalaryReportMonthHistoryViewModel
                        {
                            Year = cursor.Year,
                            Month = cursor.Month,
                            MonthName = cursor.ToString("MMMM")
                        };

                        foreach (var name in allowanceNames)
                            history.Allowances[name] = 0;

                        foreach (var name in deductionNames)
                            history.Deductions[name] = 0;

                        history.BasicSalary = await LoadSavedBasic(
                            connection, employee.EmployeeCode, cursor.Year, cursor.Month);

                        await LoadSavedPayTrnFtAllowances(
                            connection, history, employee.EmployeeCode, cursor.Year, cursor.Month);

                        await LoadSavedPayTrnFtDeductions(
                            connection, history, employee.EmployeeCode, cursor.Year, cursor.Month);

                        history.TotalAllowance = Math.Round(history.Allowances.Values.Sum(), 2);
                        history.GrossSalary = Math.Round(history.BasicSalary + history.TotalAllowance, 2);
                        history.TotalDeduction = Math.Round(history.Deductions.Values.Sum(), 2);
                        history.NetSalary = Math.Round(history.GrossSalary - history.TotalDeduction, 2);

                        employee.History.Add(history);

                        employee.BasicSalary += history.BasicSalary;
                        employee.TotalAllowance += history.TotalAllowance;
                        employee.GrossSalary += history.GrossSalary;
                        employee.TotalDeduction += history.TotalDeduction;
                        employee.NetSalary += history.NetSalary;

                        foreach (var name in allowanceNames)
                            employee.Allowances[name] += DictionaryAmount(history.Allowances, name);

                        foreach (var name in deductionNames)
                            employee.Deductions[name] += DictionaryAmount(history.Deductions, name);
                    }

                    employee.BasicSalary = Math.Round(employee.BasicSalary, 2);
                    employee.TotalAllowance = Math.Round(employee.TotalAllowance, 2);
                    employee.GrossSalary = Math.Round(employee.GrossSalary, 2);
                    employee.TotalDeduction = Math.Round(employee.TotalDeduction, 2);
                    employee.NetSalary = Math.Round(employee.NetSalary, 2);

                    foreach (var name in allowanceNames)
                        employee.Allowances[name] = Math.Round(employee.Allowances[name], 2);

                    foreach (var name in deductionNames)
                        employee.Deductions[name] = Math.Round(employee.Deductions[name], 2);
                }

                ViewBag.BranchCode = branchCode;
                ViewBag.BranchName = branchName;
                ViewBag.FromYear = fromYear;
                ViewBag.FromMonth = fromMonth;
                ViewBag.ToYear = toYear;
                ViewBag.ToMonth = toMonth;
                ViewBag.FromMonthName = from.ToString("MMMM");
                ViewBag.ToMonthName = to.ToString("MMMM");
                // Hide only the columns that were hidden in the currently printed branch.
                var hiddenAllowanceSet = new HashSet<string>(
                    hiddenAllowanceNames ?? new List<string>(),
                    StringComparer.OrdinalIgnoreCase);

                var hiddenDeductionSet = new HashSet<string>(
                    hiddenDeductionNames ?? new List<string>(),
                    StringComparer.OrdinalIgnoreCase);

                var printableAllowanceNames = allowanceNames
                    .Where(name => !hiddenAllowanceSet.Contains(name))
                    .ToList();

                var printableDeductionNames = deductionNames
                    .Where(name => !hiddenDeductionSet.Contains(name))
                    .ToList();

                ViewBag.AllowanceNames = printableAllowanceNames;
                ViewBag.DeductionNames = printableDeductionNames;
                ViewBag.PrintDate = DateTime.Now;

                return View("Print", employees);
            }
            finally
            {
                if (close && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }

        private static double DictionaryAmount(
            Dictionary<string, double> values,
            string key)
        {
            return values.TryGetValue(key, out var value) ? value : 0;
        }

        // Saved Basic for exact month/year.
        // If dbo.paytrn has no row for that month, 0.00 is returned.
        private async Task<double> LoadSavedBasic(
            DbConnection connection,
            int employeeCode,
            int year,
            int month)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT TOP 1 ISNULL(TRY_CONVERT(FLOAT,basic),0)
FROM dbo.paytrn
WHERE TRY_CONVERT(INT,code)=@EmployeeCode
  AND TRY_CONVERT(INT,[year])=@Year
  AND TRY_CONVERT(INT,[month])=@Month;";

            AddParameter(command, "@EmployeeCode", employeeCode);
            AddParameter(command, "@Year", year);
            AddParameter(command, "@Month", month);

            var value = await command.ExecuteScalarAsync();
            return value == null || value == DBNull.Value ? 0 : Convert.ToDouble(value);
        }

        // Exact saved allowances from dbo.paytrnft, flag = 1.
        private async Task LoadSavedPayTrnFtAllowances(
            DbConnection connection,
            SalaryReportMonthHistoryViewModel history,
            int employeeCode,
            int year,
            int month)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT
    LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(am.short_name)),''),am.name))) ComponentName,
    SUM(ISNULL(TRY_CONVERT(FLOAT,pt.amt),0)) Amount
FROM dbo.paytrnft pt
INNER JOIN dbo.allwmast am
    ON TRY_CONVERT(INT,am.code)=TRY_CONVERT(INT,pt.allw_dedu_code)
WHERE TRY_CONVERT(INT,pt.empl)=@EmployeeCode
  AND TRY_CONVERT(INT,pt.[year])=@Year
  AND TRY_CONVERT(INT,pt.[month])=@Month
  AND TRY_CONVERT(INT,pt.flag)=1
GROUP BY LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(am.short_name)),''),am.name)));";

            AddParameter(command, "@EmployeeCode", employeeCode);
            AddParameter(command, "@Year", year);
            AddParameter(command, "@Month", month);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string name = Convert.ToString(reader["ComponentName"])?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                history.Allowances[name] = Number(reader["Amount"]);
            }
        }

        // Exact saved deductions from dbo.paytrnft, flag = 2.
        private async Task LoadSavedPayTrnFtDeductions(
            DbConnection connection,
            SalaryReportMonthHistoryViewModel history,
            int employeeCode,
            int year,
            int month)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT
    LTRIM(RTRIM(ISNULL(dm.name,''))) ComponentName,
    SUM(ISNULL(TRY_CONVERT(FLOAT,pt.amt),0)) Amount
FROM dbo.paytrnft pt
INNER JOIN dbo.dedumast dm
    ON TRY_CONVERT(INT,dm.code)=TRY_CONVERT(INT,pt.allw_dedu_code)
WHERE TRY_CONVERT(INT,pt.empl)=@EmployeeCode
  AND TRY_CONVERT(INT,pt.[year])=@Year
  AND TRY_CONVERT(INT,pt.[month])=@Month
  AND TRY_CONVERT(INT,pt.flag)=2
GROUP BY LTRIM(RTRIM(ISNULL(dm.name,'')));";

            AddParameter(command, "@EmployeeCode", employeeCode);
            AddParameter(command, "@Year", year);
            AddParameter(command, "@Month", month);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string name = Convert.ToString(reader["ComponentName"])?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                history.Deductions[name] = Number(reader["Amount"]);
            }
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

                if (!string.IsNullOrWhiteSpace(name) &&
                    !result.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(name);
                }
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

                if (!string.IsNullOrWhiteSpace(name) &&
                    !result.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(name);
                }
            }

            return result;
        }

        private async Task LoadEmployeeAllowances(
            DbConnection connection,
            SalaryReportEmployeeRowViewModel employee,
            DateTime monthStart,
            DateTime monthEnd)
        {
            using var command = connection.CreateCommand();

            command.CommandText = @"
SELECT LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(am.short_name)),''),am.name))) ComponentName,
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

        private async Task LoadEmployeeDeductions(
            DbConnection connection,
            SalaryReportEmployeeRowViewModel employee,
            DateTime monthStart,
            DateTime monthEnd)
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

        // READ ONLY: Professional Tax threshold from dbo.daptsys.from1.
        // This method only SELECTs data; it does not update the database.
        private async Task<double> LoadProfessionalTax(DbConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT TOP 1
       ISNULL(TRY_CONVERT(FLOAT, [from1]), 0)
FROM dbo.daptsys;";

            var result = await command.ExecuteScalarAsync();

            return result == null || result == DBNull.Value
                ? 0
                : Convert.ToDouble(result);
        }

        // Same Professional Tax calculation as Pay Calculation.
        private static void SetProfessionalTax(
            SalaryReportEmployeeRowViewModel employee,
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
                .FirstOrDefault(x =>
                    x.Equals(
                        "PROFESSIONAL TAX",
                        StringComparison.OrdinalIgnoreCase));

            if (key != null)
                employee.Deductions[key] = professionalTax;
            else
                employee.Deductions["PROFESSIONAL TAX"] = professionalTax;
        }

        // Same agreed calculation rule as Pay Calculation.
        private static double Calculate(double basic, double fixedAmount, double percent)
        {
            double amount = fixedAmount > 0
                ? fixedAmount
                : percent > 0
                    ? basic * percent / 100.0
                    : 0;

            return Math.Round(amount, 2);
        }


        // ============================================================
        // READ ONLY: Selected Allowance / Deduction printable reports
        // ============================================================
        // ============================================================
        // READ ONLY: Combined selected Allowance + Deduction print report
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> CombinedPrint(
            int fromYear, int fromMonth, int toYear, int toMonth,
            List<int> selectedAllowanceCodes,
            List<int> selectedDeductionCodes)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            if (fromYear < 1900 || toYear < 1900 ||
                fromMonth < 1 || fromMonth > 12 ||
                toMonth < 1 || toMonth > 12)
                return BadRequest("Please select a valid From/To period.");

            var from = new DateTime(fromYear, fromMonth, 1);
            var to = new DateTime(toYear, toMonth, 1);

            if (from > to)
                return BadRequest("From Month cannot be after To Month.");

            selectedAllowanceCodes ??= new List<int>();
            selectedDeductionCodes ??= new List<int>();

            if (selectedAllowanceCodes.Count == 0 && selectedDeductionCodes.Count == 0)
                return BadRequest("Please select at least one Allowance or Deduction.");

            bool showBasic = selectedAllowanceCodes.Contains(SalaryReportComponentViewModel.BasicCode);

            var allowanceOptions = await LoadComponentOptions(true);
            var deductionOptions = await LoadComponentOptions(false);

            var selectedAllowances = allowanceOptions
                .Where(x => x.Code != SalaryReportComponentViewModel.BasicCode &&
                            selectedAllowanceCodes.Contains(x.Code))
                .ToList();

            var selectedDeductions = deductionOptions
                .Where(x => selectedDeductionCodes.Contains(x.Code))
                .ToList();

            if (!showBasic && selectedAllowances.Count == 0 && selectedDeductions.Count == 0)
                return BadRequest("No valid components selected.");

            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;

            try
            {
                if (close) await connection.OpenAsync();

                var employees = new List<SalaryReportEmployeeRowViewModel>();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT DISTINCT
       p.code EmployeeCode,
       ISNULL(p.name,'') EmployeeName
FROM dbo.PayMast p
INNER JOIN dbo.paytrn t
   ON TRY_CONVERT(INT,t.code)=TRY_CONVERT(INT,p.code)
  AND ((TRY_CONVERT(INT,t.[year])*12+TRY_CONVERT(INT,t.[month]))
       BETWEEN (@FromYear*12+@FromMonth) AND (@ToYear*12+@ToMonth))
ORDER BY p.code;";

                    AddParameter(command, "@FromYear", fromYear);
                    AddParameter(command, "@FromMonth", fromMonth);
                    AddParameter(command, "@ToYear", toYear);
                    AddParameter(command, "@ToMonth", toMonth);

                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var employee = new SalaryReportEmployeeRowViewModel
                        {
                            EmployeeCode = Convert.ToInt32(reader["EmployeeCode"]),
                            EmployeeName = Convert.ToString(reader["EmployeeName"])?.Trim() ?? ""
                        };

                        foreach (var item in selectedAllowances)
                            employee.Allowances[item.Name] = 0;

                        foreach (var item in selectedDeductions)
                            employee.Deductions[item.Name] = 0;

                        employees.Add(employee);
                    }
                }

                foreach (var employee in employees)
                {
                    for (var cursor = from; cursor <= to; cursor = cursor.AddMonths(1))
                    {
                        if (showBasic)
                        {
                            employee.BasicSalary += await LoadSavedBasic(
                                connection, employee.EmployeeCode, cursor.Year, cursor.Month);
                        }

                        if (selectedAllowances.Count > 0)
                        {
                            using var allowanceCommand = connection.CreateCommand();
                            allowanceCommand.CommandText = @"
SELECT TRY_CONVERT(INT,pt.allw_dedu_code) Code,
       SUM(ISNULL(TRY_CONVERT(FLOAT,pt.amt),0)) Amount
FROM dbo.paytrnft pt
WHERE TRY_CONVERT(INT,pt.empl)=@Emp
  AND TRY_CONVERT(INT,pt.[year])=@Year
  AND TRY_CONVERT(INT,pt.[month])=@Month
  AND TRY_CONVERT(INT,pt.flag)=1
GROUP BY TRY_CONVERT(INT,pt.allw_dedu_code);";
                            AddParameter(allowanceCommand, "@Emp", employee.EmployeeCode);
                            AddParameter(allowanceCommand, "@Year", cursor.Year);
                            AddParameter(allowanceCommand, "@Month", cursor.Month);

                            using var reader = await allowanceCommand.ExecuteReaderAsync();
                            while (await reader.ReadAsync())
                            {
                                int code = Convert.ToInt32(reader["Code"]);
                                var option = selectedAllowances.FirstOrDefault(x => x.Code == code);
                                if (option != null)
                                    employee.Allowances[option.Name] += Number(reader["Amount"]);
                            }
                        }

                        if (selectedDeductions.Count > 0)
                        {
                            using var deductionCommand = connection.CreateCommand();
                            deductionCommand.CommandText = @"
SELECT TRY_CONVERT(INT,pt.allw_dedu_code) Code,
       SUM(ISNULL(TRY_CONVERT(FLOAT,pt.amt),0)) Amount
FROM dbo.paytrnft pt
WHERE TRY_CONVERT(INT,pt.empl)=@Emp
  AND TRY_CONVERT(INT,pt.[year])=@Year
  AND TRY_CONVERT(INT,pt.[month])=@Month
  AND TRY_CONVERT(INT,pt.flag)=2
GROUP BY TRY_CONVERT(INT,pt.allw_dedu_code);";
                            AddParameter(deductionCommand, "@Emp", employee.EmployeeCode);
                            AddParameter(deductionCommand, "@Year", cursor.Year);
                            AddParameter(deductionCommand, "@Month", cursor.Month);

                            using var reader = await deductionCommand.ExecuteReaderAsync();
                            while (await reader.ReadAsync())
                            {
                                int code = Convert.ToInt32(reader["Code"]);
                                var option = selectedDeductions.FirstOrDefault(x => x.Code == code);
                                if (option != null)
                                    employee.Deductions[option.Name] += Number(reader["Amount"]);
                            }
                        }
                    }

                    employee.BasicSalary = Math.Round(employee.BasicSalary, 2);
                    foreach (var key in employee.Allowances.Keys.ToList())
                        employee.Allowances[key] = Math.Round(employee.Allowances[key], 2);
                    foreach (var key in employee.Deductions.Keys.ToList())
                        employee.Deductions[key] = Math.Round(employee.Deductions[key], 2);
                }

                ViewBag.FromMonthName = from.ToString("MMMM");
                ViewBag.FromYear = fromYear;
                ViewBag.ToMonthName = to.ToString("MMMM");
                ViewBag.ToYear = toYear;
                ViewBag.ShowBasic = showBasic;
                ViewBag.AllowanceNames = selectedAllowances.Select(x => x.Name).ToList();
                ViewBag.DeductionNames = selectedDeductions.Select(x => x.Name).ToList();
                ViewBag.PrintDate = DateTime.Now;

                return View("CombinedPrint", employees);
            }
            finally
            {
                if (close && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }

        [HttpGet]
        public async Task<IActionResult> AllowancePrint(int fromYear, int fromMonth, int toYear, int toMonth, List<int> selectedCodes)
        {
            return await SelectedComponentPrint(fromYear, fromMonth, toYear, toMonth, selectedCodes, true);
        }

        [HttpGet]
        public async Task<IActionResult> DeductionPrint(int fromYear, int fromMonth, int toYear, int toMonth, List<int> selectedCodes)
        {
            return await SelectedComponentPrint(fromYear, fromMonth, toYear, toMonth, selectedCodes, false);
        }

        private async Task<IActionResult> SelectedComponentPrint(int fromYear, int fromMonth, int toYear, int toMonth, List<int> selectedCodes, bool allowance)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            if (fromYear < 1900 || toYear < 1900 || fromMonth < 1 || fromMonth > 12 || toMonth < 1 || toMonth > 12)
                return BadRequest("Please select a valid From/To period.");
            var from = new DateTime(fromYear, fromMonth, 1);
            var to = new DateTime(toYear, toMonth, 1);
            if (from > to) return BadRequest("From Month cannot be after To Month.");
            if (selectedCodes == null || selectedCodes.Count == 0)
                return BadRequest(allowance ? "Please select at least one Allowance." : "Please select at least one Deduction.");

            // Basic is a virtual Allowance option. It does not come from dbo.allwmast.
            bool showBasic = allowance && selectedCodes.Contains(SalaryReportComponentViewModel.BasicCode);

            var allOptions = await LoadComponentOptions(allowance);
            var selected = allOptions
                .Where(x => x.Code != SalaryReportComponentViewModel.BasicCode &&
                            selectedCodes.Contains(x.Code))
                .ToList();

            // For Allowance report, Basic by itself is also a valid selection.
            if (!showBasic && selected.Count == 0)
                return BadRequest("No valid components selected.");

            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;
            try
            {
                if (close) await connection.OpenAsync();
                var employees = new List<SalaryReportEmployeeRowViewModel>();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT DISTINCT p.code EmployeeCode, ISNULL(p.name,'') EmployeeName,
       TRY_CONVERT(INT,p.grad_Code) GradeCode, ISNULL(g.name,'') GradeName
FROM dbo.PayMast p
LEFT JOIN dbo.gradmast g ON TRY_CONVERT(INT,g.code)=TRY_CONVERT(INT,p.grad_Code)
INNER JOIN dbo.paytrn t ON TRY_CONVERT(INT,t.code)=TRY_CONVERT(INT,p.code)
 AND ((TRY_CONVERT(INT,t.[year])*12+TRY_CONVERT(INT,t.[month]))
 BETWEEN (@FromYear*12+@FromMonth) AND (@ToYear*12+@ToMonth))
ORDER BY p.code;";
                    AddParameter(command, "@FromYear", fromYear); AddParameter(command, "@FromMonth", fromMonth);
                    AddParameter(command, "@ToYear", toYear); AddParameter(command, "@ToMonth", toMonth);
                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var e = new SalaryReportEmployeeRowViewModel
                        {
                            EmployeeCode = Convert.ToInt32(reader["EmployeeCode"]),
                            EmployeeName = Convert.ToString(reader["EmployeeName"])?.Trim() ?? "",
                            GradeCode = NullableInt(reader["GradeCode"]),
                            GradeName = Convert.ToString(reader["GradeName"])?.Trim() ?? ""
                        };
                        foreach (var x in selected) { if (allowance) e.Allowances[x.Name] = 0; else e.Deductions[x.Name] = 0; }
                        employees.Add(e);
                    }
                }

                foreach (var e in employees)
                {
                    for (var cursor = from; cursor <= to; cursor = cursor.AddMonths(1))
                    {
                        // Fetch Basic only when admin selected Basic in Selected Allowances.
                        if (showBasic)
                        {
                            e.BasicSalary += await LoadSavedBasic(
                                connection, e.EmployeeCode, cursor.Year, cursor.Month);
                        }

                        using var cmd = connection.CreateCommand();
                        cmd.CommandText = allowance ? @"
SELECT TRY_CONVERT(INT,pt.allw_dedu_code) Code, SUM(ISNULL(TRY_CONVERT(FLOAT,pt.amt),0)) Amount
FROM dbo.paytrnft pt WHERE TRY_CONVERT(INT,pt.empl)=@Emp AND TRY_CONVERT(INT,pt.[year])=@Year
AND TRY_CONVERT(INT,pt.[month])=@Month AND TRY_CONVERT(INT,pt.flag)=1 GROUP BY TRY_CONVERT(INT,pt.allw_dedu_code);" : @"
SELECT TRY_CONVERT(INT,pt.allw_dedu_code) Code, SUM(ISNULL(TRY_CONVERT(FLOAT,pt.amt),0)) Amount
FROM dbo.paytrnft pt WHERE TRY_CONVERT(INT,pt.empl)=@Emp AND TRY_CONVERT(INT,pt.[year])=@Year
AND TRY_CONVERT(INT,pt.[month])=@Month AND TRY_CONVERT(INT,pt.flag)=2 GROUP BY TRY_CONVERT(INT,pt.allw_dedu_code);";
                        AddParameter(cmd, "@Emp", e.EmployeeCode); AddParameter(cmd, "@Year", cursor.Year); AddParameter(cmd, "@Month", cursor.Month);
                        using var r = await cmd.ExecuteReaderAsync();
                        while (await r.ReadAsync())
                        {
                            int code = Convert.ToInt32(r["Code"]); var opt = selected.FirstOrDefault(x => x.Code == code); if (opt == null) continue;
                            if (allowance) e.Allowances[opt.Name] += Number(r["Amount"]); else e.Deductions[opt.Name] += Number(r["Amount"]);
                        }
                    }
                    e.BasicSalary = Math.Round(e.BasicSalary, 2);
                    e.TotalAllowance = Math.Round(e.Allowances.Values.Sum(), 2);
                    e.TotalDeduction = Math.Round(e.Deductions.Values.Sum(), 2);
                }

                ViewBag.FromMonthName = from.ToString("MMMM"); ViewBag.FromYear = fromYear;
                ViewBag.ToMonthName = to.ToString("MMMM"); ViewBag.ToYear = toYear;
                ViewBag.ComponentNames = selected.Select(x => x.Name).ToList();
                ViewBag.ShowBasic = showBasic;
                ViewBag.PrintDate = DateTime.Now;
                return View(allowance ? "AllowancePrint" : "DeductionPrint", employees);
            }
            finally { if (close && connection.State == ConnectionState.Open) await connection.CloseAsync(); }
        }

        private async Task<List<SalaryReportComponentViewModel>> LoadComponentOptions(bool allowance)
        {
            var result = new List<SalaryReportComponentViewModel>();

            // Basic must appear FIRST in Selected Allowances.
            // It is a virtual option; its amount is read from dbo.paytrn only when selected.
            if (allowance)
            {
                result.Add(new SalaryReportComponentViewModel
                {
                    Code = SalaryReportComponentViewModel.BasicCode,
                    Name = "Basic"
                });
            }

            var connection = _context.Database.GetDbConnection(); bool close = connection.State != ConnectionState.Open;
            try
            {
                if (close) await connection.OpenAsync(); using var command = connection.CreateCommand();
                command.CommandText = allowance ? @"
SELECT TRY_CONVERT(INT,code) Code, LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(short_name)),''),name))) Name
FROM dbo.allwmast WHERE TRY_CONVERT(INT,code) IS NOT NULL ORDER BY TRY_CONVERT(INT,code);" : @"
SELECT TRY_CONVERT(INT,code) Code, LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(short_name)),''),name))) Name
FROM dbo.dedumast WHERE TRY_CONVERT(INT,code) IS NOT NULL ORDER BY TRY_CONVERT(INT,code);";
                using var reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync())
                { var name = Convert.ToString(reader["Name"])?.Trim() ?? ""; if (!string.IsNullOrWhiteSpace(name)) result.Add(new SalaryReportComponentViewModel { Code = Convert.ToInt32(reader["Code"]), Name = name }); }
            }
            finally { if (close && connection.State == ConnectionState.Open) await connection.CloseAsync(); }
            return result;
        }

        private bool IsLoggedIn() =>
            !string.IsNullOrWhiteSpace(
                HttpContext.Session.GetString("LoginId"));

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

        private static int? NullableInt(object value) =>
            value == DBNull.Value
                ? null
                : Convert.ToInt32(value);

        private static double Number(object value) =>
            value == DBNull.Value
                ? 0
                : Convert.ToDouble(value);
    }
}
