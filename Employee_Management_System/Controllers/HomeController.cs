using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading.Tasks;
using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Employee_Management_System.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            int? divisionCode = null,
            int? gradeCode = null,
            string gender = "",
            int? salaryYear = null,
            int? salaryMonth = null,
            int? grossBranchCode = null,
            DateTime? grossFromDate = null,
            DateTime? grossToDate = null)
        {
            var model = new EmployeeDashboardViewModel
            {
                SelectedDivision = divisionCode,
                SelectedGrade = gradeCode,
                SelectedGender = (gender ?? "").Trim().ToUpperInvariant()
            };

            var connection = _context.Database.GetDbConnection();
            bool closeConnection = connection.State != ConnectionState.Open;

            try
            {
                if (closeConnection)
                    await connection.OpenAsync();

                // 1. Working Date
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT TOP 1 CAST([date] AS date)
FROM dbo.[SYSTEM];";

                    object? result = await command.ExecuteScalarAsync();

                    model.WorkingDate =
                        result != null && result != DBNull.Value
                            ? Convert.ToDateTime(result).Date
                            : DateTime.Today;
                }

                DateTime workingDate =
                    model.WorkingDate?.Date ?? DateTime.Today;

                model.SelectedSalaryYear =
                    salaryYear ?? workingDate.Year;

                model.SelectedSalaryMonth =
                    salaryMonth.HasValue &&
                    salaryMonth.Value >= 1 &&
                    salaryMonth.Value <= 12
                        ? salaryMonth.Value
                        : workingDate.Month;

                // Independent selection for the Branch Wise Total Gross Salary bar chart.
                // null branch means All Branches.
                model.SelectedGrossSalaryBranch = grossBranchCode;

                // Default Gross Salary chart date range:
                // To Date   = Login / Working Date
                // From Date = exactly 12 months before Login / Working Date
                // These defaults apply only when the page is opened without
                // manually supplied grossFromDate / grossToDate values.
                DateTime defaultToDate = workingDate.Date;
                DateTime defaultFromDate = defaultToDate.AddMonths(-12);

                model.GrossSalaryFromDate =
                    (grossFromDate ?? defaultFromDate).Date;

                model.GrossSalaryToDate =
                    (grossToDate ?? defaultToDate).Date;

                if (model.GrossSalaryToDate < model.GrossSalaryFromDate)
                {
                    var swap = model.GrossSalaryFromDate;
                    model.GrossSalaryFromDate = model.GrossSalaryToDate;
                    model.GrossSalaryToDate = swap;
                }

                // 2. Dashboard cards - optimized date filtering
                using (var command = connection.CreateCommand())
                {
                    // Safety margin for slower SQL Server instances.
                    // The query itself is optimized below and no CAST is applied to leave date columns.
                    command.CommandTimeout = 120;
                    command.CommandText = @"
;WITH ActiveEmployees AS
(
    SELECT
        P.code,
        UPPER(LTRIM(RTRIM(ISNULL(P.sex, '')))) AS sex
    FROM dbo.PayMast P
    INNER JOIN dbo.brncmast B
        ON B.code = P.brnc_code
    WHERE P.[type] IN (1,2,3,4,5)
      AND (@DivisionCode IS NULL OR B.Tcode = @DivisionCode)
      AND (@GradeCode IS NULL OR P.grad_Code = @GradeCode)
      AND (@Gender = '' OR UPPER(LTRIM(RTRIM(ISNULL(P.sex, '')))) = @Gender)
),
WorkingDateAbsent AS
(
    SELECT DISTINCT A.code
    FROM ActiveEmployees A
    INNER JOIN dbo.leavtrn L
        ON L.empl = A.code
    WHERE L.dmnd_from_date IS NOT NULL
      AND L.dmnd_to_date IS NOT NULL
      AND L.dmnd_from_date < DATEADD(DAY, 1, @WorkingDate)
      AND L.dmnd_to_date >= @WorkingDate
)
SELECT
    COUNT(*) AS ActiveEmployees,
    SUM(CASE WHEN A.sex = 'M' THEN 1 ELSE 0 END) AS MaleEmployees,
    SUM(CASE WHEN A.sex = 'F' THEN 1 ELSE 0 END) AS FemaleEmployees,
    SUM(CASE WHEN W.code IS NULL THEN 1 ELSE 0 END) AS TodayPresentEmployees,
    SUM(CASE WHEN W.code IS NOT NULL THEN 1 ELSE 0 END) AS TodayAbsentEmployees
FROM ActiveEmployees A
LEFT JOIN WorkingDateAbsent W
    ON W.code = A.code;";

                    AddParameter(command, "@DivisionCode", divisionCode);
                    AddParameter(command, "@GradeCode", gradeCode);
                    AddParameter(command, "@Gender", model.SelectedGender);
                    AddParameter(command, "@WorkingDate", workingDate);

                    using var reader = await command.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        model.ActiveEmployees = GetInt(reader, "ActiveEmployees");
                        model.MaleEmployees = GetInt(reader, "MaleEmployees");
                        model.FemaleEmployees = GetInt(reader, "FemaleEmployees");
                        model.TodayPresentEmployees = GetInt(reader, "TodayPresentEmployees");
                        model.TodayAbsentEmployees = GetInt(reader, "TodayAbsentEmployees");
                    }
                }

                // 3. Division dropdown - MAIN Dashboard filter
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT TRY_CONVERT(INT,code) AS code, ISNULL(name,'') AS name
FROM dbo.Division_Master
ORDER BY TRY_CONVERT(INT,code), name;";

                    using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        model.Divisions.Add(new EmployeeDashboardOptionViewModel
                        {
                            Value = Convert.ToInt32(reader["code"]),
                            Text = Convert.ToString(reader["name"])?.Trim() ?? ""
                        });
                    }
                }

                // 3A. Branch dropdown - KEEP for bottom Gross Salary filter
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT CAST(code AS INT) AS code, ISNULL(name,'') AS name
FROM dbo.brncmast
ORDER BY name, code;";

                    using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        model.Branches.Add(new EmployeeDashboardOptionViewModel
                        {
                            Value = Convert.ToInt32(reader["code"]),
                            Text = Convert.ToString(reader["name"]) ?? ""
                        });
                    }
                }

                // 4. Grade dropdown
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT CAST(code AS INT) AS code, ISNULL(name,'') AS name
FROM dbo.gradmast
ORDER BY name, code;";

                    using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        model.Grades.Add(new EmployeeDashboardOptionViewModel
                        {
                            Value = Convert.ToInt32(reader["code"]),
                            Text = Convert.ToString(reader["name"]) ?? ""
                        });
                    }
                }

                // 5. Division Wise Active Employee chart
                // Existing chart model is reused:
                // BranchCode = DivisionCode, BranchName = DivisionName.
                using (var command = connection.CreateCommand())
                {
                    command.CommandTimeout = 120;
                    command.CommandText = @"
SELECT
    TRY_CONVERT(INT,D.code) AS DivisionCode,
    ISNULL(D.name,'') AS DivisionName,
    COUNT(P.code) AS EmployeeCount
FROM dbo.Division_Master D
LEFT JOIN dbo.brncmast B
    ON TRY_CONVERT(INT,B.Tcode)=TRY_CONVERT(INT,D.code)
LEFT JOIN dbo.PayMast P
    ON TRY_CONVERT(INT,P.brnc_code)=TRY_CONVERT(INT,B.code)
   AND TRY_CONVERT(INT,P.[type]) IN (1,2,3,4,5)
   AND (@GradeCode IS NULL OR TRY_CONVERT(INT,P.grad_Code)=@GradeCode)
   AND (@Gender='' OR UPPER(LTRIM(RTRIM(ISNULL(P.sex,''))))=@Gender)
WHERE (@DivisionCode IS NULL OR TRY_CONVERT(INT,D.code)=@DivisionCode)
GROUP BY D.code,D.name
HAVING COUNT(P.code)>0
ORDER BY TRY_CONVERT(INT,D.code),D.name;";

                    AddParameter(command, "@DivisionCode", divisionCode);
                    AddParameter(command, "@GradeCode", gradeCode);
                    AddParameter(command, "@Gender", model.SelectedGender);

                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        model.BranchWiseEmployees.Add(new BranchEmployeeChartViewModel
                        {
                            BranchCode = GetInt(reader, "DivisionCode"),
                            BranchName = Convert.ToString(reader["DivisionName"])?.Trim() ?? "",
                            EmployeeCount = GetInt(reader, "EmployeeCount")
                        });
                    }
                }

                // 6. Division Wise Salary chart
                // LIVE PAY CALCULATION GROSS SALARY
                //
                // Gross Salary = WPL Adjusted Basic + Total Allowance
                // dbo.paytrn is NOT used for this chart.
                // Selected Division + Grade + Gender + Selected Month are applied.

                DateTime salaryChartMonthStart =
                    new DateTime(
                        model.SelectedSalaryYear,
                        model.SelectedSalaryMonth,
                        1);

                DateTime salaryChartMonthEnd =
                    salaryChartMonthStart.AddMonths(1).AddDays(-1);

                int salaryChartDaysInMonth =
                    DateTime.DaysInMonth(
                        model.SelectedSalaryYear,
                        model.SelectedSalaryMonth);

                // Division list for pie chart.
                var salaryChartDivisions =
                    new List<(int DivisionCode, string DivisionName)>();

                using (var command = connection.CreateCommand())
                {
                    command.CommandTimeout = 120;
                    command.CommandText = @"
SELECT
    TRY_CONVERT(INT,D.code) AS DivisionCode,
    ISNULL(D.name,'') AS DivisionName
FROM dbo.Division_Master D
WHERE (@DivisionCode IS NULL
       OR TRY_CONVERT(INT,D.code)=@DivisionCode)
ORDER BY TRY_CONVERT(INT,D.code),D.name;";

                    AddParameter(command, "@DivisionCode", divisionCode);

                    using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        salaryChartDivisions.Add((
                            GetInt(reader, "DivisionCode"),
                            Convert.ToString(reader["DivisionName"])?.Trim() ?? ""));
                    }
                }

                // Active employees for selected Division/Grade/Gender.
                var salaryChartEmployees =
                    new List<(int EmployeeCode, int DivisionCode, double OriginalBasic)>();

                using (var command = connection.CreateCommand())
                {
                    command.CommandTimeout = 120;
                    command.CommandText = @"
SELECT
    TRY_CONVERT(INT,P.code) AS EmployeeCode,
    TRY_CONVERT(INT,B.Tcode) AS DivisionCode,
    ISNULL(TRY_CONVERT(FLOAT,P.basic),0) AS OriginalBasic
FROM dbo.PayMast P
INNER JOIN dbo.brncmast B
    ON TRY_CONVERT(INT,B.code)=TRY_CONVERT(INT,P.brnc_code)
WHERE TRY_CONVERT(INT,P.[type]) IN (1,2,3,4,5)
  AND (@DivisionCode IS NULL
       OR TRY_CONVERT(INT,B.Tcode)=@DivisionCode)
  AND (@GradeCode IS NULL
       OR TRY_CONVERT(INT,P.grad_Code)=@GradeCode)
  AND (@Gender=''
       OR UPPER(LTRIM(RTRIM(ISNULL(P.sex,''))))=@Gender)
ORDER BY TRY_CONVERT(INT,P.brnc_code),
         TRY_CONVERT(INT,P.code);";

                    AddParameter(command, "@DivisionCode", divisionCode);
                    AddParameter(command, "@GradeCode", gradeCode);
                    AddParameter(command, "@Gender", model.SelectedGender);

                    using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        salaryChartEmployees.Add((
                            GetInt(reader, "EmployeeCode"),
                            GetInt(reader, "DivisionCode"),
                            GetDouble(reader, "OriginalBasic")));
                    }
                }

                // Load approved WPL for ALL selected employees in one query.
                var salaryChartWplByEmployee =
                    new Dictionary<int, double>();

                using (var command = connection.CreateCommand())
                {
                    command.CommandTimeout = 120;
                    command.CommandText = @"
SELECT
    TRY_CONVERT(INT,L.empl) AS EmployeeCode,
    ISNULL(SUM(
        CASE
            WHEN CAST(L.sact_from_date AS date) >= @MonthStart
             AND CAST(L.sact_to_date AS date) <= @MonthEnd
             AND ISNULL(TRY_CONVERT(FLOAT,L.Num_of_days),0) > 0
            THEN TRY_CONVERT(FLOAT,L.Num_of_days)
            ELSE DATEDIFF(
                    DAY,
                    CASE
                        WHEN CAST(L.sact_from_date AS date) < @MonthStart
                        THEN @MonthStart
                        ELSE CAST(L.sact_from_date AS date)
                    END,
                    CASE
                        WHEN CAST(L.sact_to_date AS date) > @MonthEnd
                        THEN @MonthEnd
                        ELSE CAST(L.sact_to_date AS date)
                    END
                 ) + 1
        END
    ),0) AS WplDays
FROM dbo.leavtrn L
INNER JOIN dbo.PayMast P
    ON TRY_CONVERT(INT,P.code)=TRY_CONVERT(INT,L.empl)
INNER JOIN dbo.brncmast B
    ON TRY_CONVERT(INT,B.code)=TRY_CONVERT(INT,P.brnc_code)
WHERE TRY_CONVERT(INT,L.sact_code)=10
  AND UPPER(LTRIM(RTRIM(ISNULL(L.Status,'')))) IN ('A','APPROVED')
  AND L.sact_from_date IS NOT NULL
  AND L.sact_to_date IS NOT NULL
  AND CAST(L.sact_from_date AS date) <= @MonthEnd
  AND CAST(L.sact_to_date AS date) >= @MonthStart
  AND TRY_CONVERT(INT,P.[type]) IN (1,2,3,4,5)
  AND (@DivisionCode IS NULL
       OR TRY_CONVERT(INT,B.Tcode)=@DivisionCode)
  AND (@GradeCode IS NULL
       OR TRY_CONVERT(INT,P.grad_Code)=@GradeCode)
  AND (@Gender=''
       OR UPPER(LTRIM(RTRIM(ISNULL(P.sex,''))))=@Gender)
GROUP BY TRY_CONVERT(INT,L.empl);";

                    AddParameter(command, "@MonthStart", salaryChartMonthStart.Date);
                    AddParameter(command, "@MonthEnd", salaryChartMonthEnd.Date);
                    AddParameter(command, "@DivisionCode", divisionCode);
                    AddParameter(command, "@GradeCode", gradeCode);
                    AddParameter(command, "@Gender", model.SelectedGender);

                    using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        int employeeCode = GetInt(reader, "EmployeeCode");

                        if (employeeCode > 0)
                        {
                            salaryChartWplByEmployee[employeeCode] =
                                GetDouble(reader, "WplDays");
                        }
                    }
                }

                // Load allowance rows for ALL selected employees in one query.
                var salaryChartAllowancesByEmployee =
                    new Dictionary<int, List<(double Amount, double Percent)>>();

                using (var command = connection.CreateCommand())
                {
                    command.CommandTimeout = 120;
                    command.CommandText = @"
SELECT
    TRY_CONVERT(INT,pf.empl_code) AS EmployeeCode,
    ISNULL(TRY_CONVERT(FLOAT,pf.amt),0) AS Amount,
    ISNULL(TRY_CONVERT(FLOAT,pf.[percent]),0) AS [Percent]
FROM dbo.payfoot pf
INNER JOIN dbo.allwmast am
    ON TRY_CONVERT(INT,am.code)=TRY_CONVERT(INT,pf.allw_dedu)
INNER JOIN dbo.PayMast P
    ON TRY_CONVERT(INT,P.code)=TRY_CONVERT(INT,pf.empl_code)
INNER JOIN dbo.brncmast B
    ON TRY_CONVERT(INT,B.code)=TRY_CONVERT(INT,P.brnc_code)
WHERE TRY_CONVERT(INT,pf.flag)=1
  AND (pf.[date] IS NULL OR pf.[date] <= @MonthEnd)
  AND (pf.cancel_date IS NULL OR pf.cancel_date >= @MonthStart)
  AND TRY_CONVERT(INT,P.[type]) IN (1,2,3,4,5)
  AND (@DivisionCode IS NULL
       OR TRY_CONVERT(INT,B.Tcode)=@DivisionCode)
  AND (@GradeCode IS NULL
       OR TRY_CONVERT(INT,P.grad_Code)=@GradeCode)
  AND (@Gender=''
       OR UPPER(LTRIM(RTRIM(ISNULL(P.sex,''))))=@Gender)
ORDER BY TRY_CONVERT(INT,pf.empl_code),
         TRY_CONVERT(INT,pf.allw_dedu);";

                    AddParameter(command, "@MonthStart", salaryChartMonthStart);
                    AddParameter(command, "@MonthEnd", salaryChartMonthEnd);
                    AddParameter(command, "@DivisionCode", divisionCode);
                    AddParameter(command, "@GradeCode", gradeCode);
                    AddParameter(command, "@Gender", model.SelectedGender);

                    using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        int employeeCode = GetInt(reader, "EmployeeCode");

                        if (employeeCode <= 0)
                            continue;

                        if (!salaryChartAllowancesByEmployee.TryGetValue(
                                employeeCode,
                                out var allowanceList))
                        {
                            allowanceList =
                                new List<(double Amount, double Percent)>();

                            salaryChartAllowancesByEmployee[employeeCode] =
                                allowanceList;
                        }

                        allowanceList.Add((
                            GetDouble(reader, "Amount"),
                            GetDouble(reader, "Percent")));
                    }
                }

                // Calculate live Gross Salary employee-wise and SUM division-wise.
                var salaryChartGrossByDivision =
                    new Dictionary<int, double>();

                foreach (var employee in salaryChartEmployees)
                {
                    double wplDays =
                        salaryChartWplByEmployee.TryGetValue(
                            employee.EmployeeCode,
                            out var loadedWplDays)
                            ? loadedWplDays
                            : 0;

                    wplDays =
                        Math.Max(
                            0,
                            Math.Min(
                                wplDays,
                                salaryChartDaysInMonth));

                    double perDay =
                        salaryChartDaysInMonth > 0
                            ? employee.OriginalBasic /
                              salaryChartDaysInMonth
                            : 0;

                    double payableBasic =
                        Math.Round(
                            Math.Max(
                                0,
                                employee.OriginalBasic -
                                (perDay * wplDays)),
                            2);

                    double totalAllowance = 0;

                    if (salaryChartAllowancesByEmployee.TryGetValue(
                            employee.EmployeeCode,
                            out var allowanceRows))
                    {
                        foreach (var allowance in allowanceRows)
                        {
                            double amount =
                                allowance.Amount > 0
                                    ? allowance.Amount
                                    : allowance.Percent > 0
                                        ? payableBasic *
                                          allowance.Percent / 100.0
                                        : 0;

                            totalAllowance +=
                                Math.Round(amount, 2);
                        }
                    }

                    totalAllowance =
                        Math.Round(totalAllowance, 2);

                    double grossSalary =
                        Math.Round(
                            payableBasic + totalAllowance,
                            2);

                    if (!salaryChartGrossByDivision.ContainsKey(
                            employee.DivisionCode))
                    {
                        salaryChartGrossByDivision[employee.DivisionCode] = 0;
                    }

                    salaryChartGrossByDivision[employee.DivisionCode] +=
                        grossSalary;
                }

                // Add Division-wise Gross Salary totals to existing chart model.
                foreach (var division in salaryChartDivisions)
                {
                    double divisionGrossSalary =
                        salaryChartGrossByDivision.TryGetValue(
                            division.DivisionCode,
                            out var loadedDivisionGross)
                            ? Math.Round(loadedDivisionGross, 2)
                            : 0;

                    model.BranchWiseSalaries.Add(
                        new BranchSalaryChartViewModel
                        {
                            BranchCode = division.DivisionCode,
                            BranchName = division.DivisionName,
                            Salary = divisionGrossSalary
                        });
                }

                // 7. MONTH WISE TOTAL GROSS SALARY - LIVE Pay Calculation
                // OPTIMIZED VERSION:
                // - dbo.paytrn is NOT used here.
                // - Employees are loaded ONCE.
                // - WPL is loaded in ONE grouped query per month.
                // - Allowances are loaded in ONE query per month.
                // This removes thousands of employee-by-employee SQL calls.

                var grossEmployees =
                    new List<(int EmployeeCode, double OriginalBasic)>();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT
    TRY_CONVERT(INT,p.code) AS EmployeeCode,
    ISNULL(TRY_CONVERT(FLOAT,p.basic),0) AS OriginalBasicSalary
FROM dbo.PayMast p
WHERE (@GrossBranchCode IS NULL
       OR TRY_CONVERT(INT,p.brnc_code)=@GrossBranchCode)
ORDER BY TRY_CONVERT(INT,p.code);";

                    AddParameter(
                        command,
                        "@GrossBranchCode",
                        model.SelectedGrossSalaryBranch);

                    using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        grossEmployees.Add((
                            GetInt(reader, "EmployeeCode"),
                            GetDouble(reader, "OriginalBasicSalary")));
                    }
                }

                DateTime monthCursor =
                    new DateTime(
                        model.GrossSalaryFromDate.Year,
                        model.GrossSalaryFromDate.Month,
                        1);

                DateTime lastMonth =
                    new DateTime(
                        model.GrossSalaryToDate.Year,
                        model.GrossSalaryToDate.Month,
                        1);

                while (monthCursor <= lastMonth)
                {
                    DateTime monthStart = monthCursor;
                    DateTime monthEnd = monthStart.AddMonths(1).AddDays(-1);
                    int daysInMonth =
                        DateTime.DaysInMonth(monthStart.Year, monthStart.Month);

                    // ----------------------------------------------------
                    // A. Load ALL approved WPL days for this month at once
                    // ----------------------------------------------------
                    var wplByEmployee = new Dictionary<int, double>();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
SELECT
    TRY_CONVERT(INT,L.empl) AS EmployeeCode,
    ISNULL(SUM(
        CASE
            WHEN CAST(L.sact_from_date AS date) >= @MonthStart
             AND CAST(L.sact_to_date AS date) <= @MonthEnd
             AND ISNULL(TRY_CONVERT(FLOAT,L.Num_of_days),0) > 0
            THEN TRY_CONVERT(FLOAT,L.Num_of_days)
            ELSE DATEDIFF(
                    DAY,
                    CASE
                        WHEN CAST(L.sact_from_date AS date) < @MonthStart
                        THEN @MonthStart
                        ELSE CAST(L.sact_from_date AS date)
                    END,
                    CASE
                        WHEN CAST(L.sact_to_date AS date) > @MonthEnd
                        THEN @MonthEnd
                        ELSE CAST(L.sact_to_date AS date)
                    END
                 ) + 1
        END
    ),0) AS WplDays
FROM dbo.leavtrn L
INNER JOIN dbo.PayMast P
    ON TRY_CONVERT(INT,P.code)=TRY_CONVERT(INT,L.empl)
WHERE TRY_CONVERT(INT,L.sact_code)=10
  AND UPPER(LTRIM(RTRIM(ISNULL(L.Status,'')))) IN ('A','APPROVED')
  AND L.sact_from_date IS NOT NULL
  AND L.sact_to_date IS NOT NULL
  AND CAST(L.sact_from_date AS date) <= @MonthEnd
  AND CAST(L.sact_to_date AS date) >= @MonthStart
  AND (@GrossBranchCode IS NULL
       OR TRY_CONVERT(INT,P.brnc_code)=@GrossBranchCode)
GROUP BY TRY_CONVERT(INT,L.empl);";

                        AddParameter(command, "@MonthStart", monthStart.Date);
                        AddParameter(command, "@MonthEnd", monthEnd.Date);
                        AddParameter(
                            command,
                            "@GrossBranchCode",
                            model.SelectedGrossSalaryBranch);

                        using var reader = await command.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            int employeeCode = GetInt(reader, "EmployeeCode");
                            double wplDays = GetDouble(reader, "WplDays");

                            if (employeeCode > 0)
                                wplByEmployee[employeeCode] = wplDays;
                        }
                    }

                    // ----------------------------------------------------
                    // B. Load ALL allowance rows for this month at once
                    // ----------------------------------------------------
                    var allowancesByEmployee =
                        new Dictionary<int, List<(double Amount, double Percent)>>();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
SELECT
    TRY_CONVERT(INT,pf.empl_code) AS EmployeeCode,
    ISNULL(TRY_CONVERT(FLOAT,pf.amt),0) AS Amount,
    ISNULL(TRY_CONVERT(FLOAT,pf.[percent]),0) AS [Percent]
FROM dbo.payfoot pf
INNER JOIN dbo.allwmast am
    ON TRY_CONVERT(INT,am.code)=TRY_CONVERT(INT,pf.allw_dedu)
INNER JOIN dbo.PayMast p
    ON TRY_CONVERT(INT,p.code)=TRY_CONVERT(INT,pf.empl_code)
WHERE TRY_CONVERT(INT,pf.flag)=1
  AND (pf.[date] IS NULL OR pf.[date] <= @MonthEnd)
  AND (pf.cancel_date IS NULL OR pf.cancel_date >= @MonthStart)
  AND (@GrossBranchCode IS NULL
       OR TRY_CONVERT(INT,p.brnc_code)=@GrossBranchCode)
ORDER BY TRY_CONVERT(INT,pf.empl_code),
         TRY_CONVERT(INT,pf.allw_dedu);";

                        AddParameter(command, "@MonthStart", monthStart);
                        AddParameter(command, "@MonthEnd", monthEnd);
                        AddParameter(
                            command,
                            "@GrossBranchCode",
                            model.SelectedGrossSalaryBranch);

                        using var reader = await command.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            int employeeCode = GetInt(reader, "EmployeeCode");

                            if (employeeCode <= 0)
                                continue;

                            if (!allowancesByEmployee.TryGetValue(
                                    employeeCode,
                                    out var allowanceList))
                            {
                                allowanceList =
                                    new List<(double Amount, double Percent)>();

                                allowancesByEmployee[employeeCode] =
                                    allowanceList;
                            }

                            allowanceList.Add((
                                GetDouble(reader, "Amount"),
                                GetDouble(reader, "Percent")));
                        }
                    }

                    // ----------------------------------------------------
                    // C. Calculate in memory using the SAME formula
                    // ----------------------------------------------------
                    double monthGrossTotal = 0;

                    foreach (var employee in grossEmployees)
                    {
                        double wplDays =
                            wplByEmployee.TryGetValue(
                                employee.EmployeeCode,
                                out var loadedWplDays)
                                ? loadedWplDays
                                : 0;

                        wplDays =
                            Math.Max(0, Math.Min(wplDays, daysInMonth));

                        double perDay =
                            daysInMonth > 0
                                ? employee.OriginalBasic / daysInMonth
                                : 0;

                        double payableBasic =
                            Math.Round(
                                Math.Max(
                                    0,
                                    employee.OriginalBasic -
                                    (perDay * wplDays)),
                                2);

                        double totalAllowance = 0;

                        if (allowancesByEmployee.TryGetValue(
                                employee.EmployeeCode,
                                out var allowanceRows))
                        {
                            foreach (var allowance in allowanceRows)
                            {
                                double amount =
                                    allowance.Amount > 0
                                        ? allowance.Amount
                                        : allowance.Percent > 0
                                            ? payableBasic *
                                              allowance.Percent / 100.0
                                            : 0;

                                totalAllowance += Math.Round(amount, 2);
                            }
                        }

                        totalAllowance = Math.Round(totalAllowance, 2);

                        double grossSalary =
                            Math.Round(
                                payableBasic + totalAllowance,
                                2);

                        monthGrossTotal += grossSalary;
                    }

                    monthGrossTotal = Math.Round(monthGrossTotal, 2);

                    model.MonthlyGrossSalaries.Add(
                        new MonthlyGrossSalaryChartViewModel
                        {
                            Year = monthStart.Year,
                            Month = monthStart.Month,
                            MonthName = monthStart.ToString(
                                "MMMM yyyy",
                                System.Globalization.CultureInfo.InvariantCulture),
                            GrossSalary = monthGrossTotal
                        });

                    model.TotalGrossSalary += monthGrossTotal;
                    monthCursor = monthCursor.AddMonths(1);
                }

                model.TotalGrossSalary =
                    Math.Round(model.TotalGrossSalary, 2);
            }
            finally
            {
                if (closeConnection &&
                    connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId =
                    Activity.Current?.Id ??
                    HttpContext.TraceIdentifier
            });
        }

        // ============================================================
        // DATABASE HELPERS
        // ============================================================
        private static void AddParameter(
            System.Data.Common.DbCommand command,
            string name,
            object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private static int GetInt(
            System.Data.Common.DbDataReader reader,
            string columnName)
        {
            var value = reader[columnName];
            return value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static double GetDouble(
            System.Data.Common.DbDataReader reader,
            string columnName)
        {
            var value = reader[columnName];
            return value == DBNull.Value ? 0 : Convert.ToDouble(value);
        }
    }
}
