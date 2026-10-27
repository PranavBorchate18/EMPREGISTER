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
;WITH DivisionOptions AS
(
    SELECT
        TRY_CONVERT(INT, D.code) AS code,
        LTRIM(RTRIM(ISNULL(D.name, ''))) AS name
    FROM dbo.Division_Master D
    WHERE TRY_CONVERT(INT, D.code) IS NOT NULL

    UNION

    SELECT DISTINCT
        TRY_CONVERT(INT, B.Tcode) AS code,
        CONCAT('Division ', TRY_CONVERT(INT, B.Tcode)) AS name
    FROM dbo.brncmast B
    WHERE TRY_CONVERT(INT, B.Tcode) IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.Division_Master)
)
SELECT code, name
FROM DivisionOptions
ORDER BY code, name;";

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

                // 5. Division Wise Employee chart
                // Data source: dbo.PayMast joined to dbo.brncmast by branch code.
                // Division is identified by brncmast.Tcode.
                using (var command = connection.CreateCommand())
                {
                    command.CommandTimeout = 120;
                    command.CommandText = @"
SELECT
    TRY_CONVERT(INT,B.Tcode) AS DivisionCode,
    COALESCE(
        NULLIF(MAX(LTRIM(RTRIM(D.name))), ''),
        CONCAT('Division ', TRY_CONVERT(INT,B.Tcode))
    ) AS DivisionName,
    COUNT(P.code) AS EmployeeCount
FROM dbo.brncmast B
LEFT JOIN dbo.Division_Master D
    ON TRY_CONVERT(INT,D.code) = TRY_CONVERT(INT,B.Tcode)
LEFT JOIN dbo.PayMast P
    ON TRY_CONVERT(INT,P.brnc_code) = TRY_CONVERT(INT,B.code)
   AND (@GradeCode IS NULL OR TRY_CONVERT(INT,P.grad_Code) = @GradeCode)
   AND (@Gender = '' OR UPPER(LTRIM(RTRIM(ISNULL(P.sex,'')))) = @Gender)
WHERE TRY_CONVERT(INT,B.Tcode) IS NOT NULL
  AND (@DivisionCode IS NULL OR TRY_CONVERT(INT,B.Tcode) = @DivisionCode)
GROUP BY TRY_CONVERT(INT,B.Tcode)
HAVING COUNT(P.code) > 0
ORDER BY TRY_CONVERT(INT,B.Tcode);";

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

                // 6. Division Wise Salary pie chart
                // Data source: saved payroll rows in dbo.paytrn for selected year/month.
                // Gross Salary = paytrn.basic + paytrn.tot_allw.
                using (var command = connection.CreateCommand())
                {
                    command.CommandTimeout = 120;
                    command.CommandText = @"
SELECT
    TRY_CONVERT(INT,B.Tcode) AS DivisionCode,
    COALESCE(
        NULLIF(MAX(LTRIM(RTRIM(D.name))), ''),
        CONCAT('Division ', TRY_CONVERT(INT,B.Tcode))
    ) AS DivisionName,
    SUM(ISNULL(TRY_CONVERT(FLOAT,T.basic),0)
      + ISNULL(TRY_CONVERT(FLOAT,T.tot_allw),0)) AS GrossSalary
FROM dbo.paytrn T
INNER JOIN dbo.brncmast B
    ON TRY_CONVERT(INT,B.code) = TRY_CONVERT(INT,T.brnc)
LEFT JOIN dbo.Division_Master D
    ON TRY_CONVERT(INT,D.code) = TRY_CONVERT(INT,B.Tcode)
LEFT JOIN dbo.PayMast P
    ON TRY_CONVERT(INT,P.code) = TRY_CONVERT(INT,T.code)
WHERE TRY_CONVERT(INT,T.[year]) = @SalaryYear
  AND TRY_CONVERT(INT,T.[month]) = @SalaryMonth
  AND TRY_CONVERT(INT,B.Tcode) IS NOT NULL
  AND (@DivisionCode IS NULL OR TRY_CONVERT(INT,B.Tcode) = @DivisionCode)
  AND (@GradeCode IS NULL OR TRY_CONVERT(INT,P.grad_Code) = @GradeCode)
  AND (@Gender = '' OR UPPER(LTRIM(RTRIM(ISNULL(P.sex,'')))) = @Gender)
GROUP BY TRY_CONVERT(INT,B.Tcode)
ORDER BY TRY_CONVERT(INT,B.Tcode);";

                    AddParameter(command, "@SalaryYear", model.SelectedSalaryYear);
                    AddParameter(command, "@SalaryMonth", model.SelectedSalaryMonth);
                    AddParameter(command, "@DivisionCode", divisionCode);
                    AddParameter(command, "@GradeCode", gradeCode);
                    AddParameter(command, "@Gender", model.SelectedGender);

                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        model.BranchWiseSalaries.Add(new BranchSalaryChartViewModel
                        {
                            BranchCode = GetInt(reader, "DivisionCode"),
                            BranchName = Convert.ToString(reader["DivisionName"])?.Trim() ?? "",
                            Salary = GetDouble(reader, "GrossSalary")
                        });
                    }
                }

                // 7. MONTH WISE TOTAL GROSS SALARY - SAVED PAYROLL (dbo.paytrn)
                // Data source: dbo.paytrn (saved payroll rows).
                // Gross Salary = paytrn.basic + paytrn.tot_allw.
                // - One grouped query for the whole date range.
                // - Every month in the selected range is shown.
                // - If a month has no payroll saved in paytrn, it shows 0.
                var paytrnGrossByMonth = new Dictionary<int, double>();

                int fromKey =
                    model.GrossSalaryFromDate.Year * 12 +
                    model.GrossSalaryFromDate.Month;

                int toKey =
                    model.GrossSalaryToDate.Year * 12 +
                    model.GrossSalaryToDate.Month;

                using (var command = connection.CreateCommand())
                {
                    command.CommandTimeout = 120;
                    command.CommandText = @"
SELECT
    TRY_CONVERT(INT,T.[year])  AS PayYear,
    TRY_CONVERT(INT,T.[month]) AS PayMonth,
    SUM(ISNULL(TRY_CONVERT(FLOAT,T.basic),0)
      + ISNULL(TRY_CONVERT(FLOAT,T.tot_allw),0)) AS GrossSalary
FROM dbo.paytrn T
WHERE TRY_CONVERT(INT,T.[year])  IS NOT NULL
  AND TRY_CONVERT(INT,T.[month]) BETWEEN 1 AND 12
  AND (TRY_CONVERT(INT,T.[year]) * 12 + TRY_CONVERT(INT,T.[month]))
        BETWEEN @FromKey AND @ToKey
  AND (@GrossBranchCode IS NULL
       OR TRY_CONVERT(INT,T.brnc) = @GrossBranchCode)
GROUP BY TRY_CONVERT(INT,T.[year]), TRY_CONVERT(INT,T.[month]);";

                    AddParameter(command, "@FromKey", fromKey);
                    AddParameter(command, "@ToKey", toKey);
                    AddParameter(
                        command,
                        "@GrossBranchCode",
                        model.SelectedGrossSalaryBranch);

                    using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        int payYear = GetInt(reader, "PayYear");
                        int payMonth = GetInt(reader, "PayMonth");

                        if (payYear <= 0 || payMonth < 1 || payMonth > 12)
                            continue;

                        paytrnGrossByMonth[payYear * 12 + payMonth] =
                            GetDouble(reader, "GrossSalary");
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
                    int monthKey = monthCursor.Year * 12 + monthCursor.Month;

                    double monthGrossTotal =
                        paytrnGrossByMonth.TryGetValue(
                            monthKey,
                            out var savedGross)
                            ? Math.Round(savedGross, 2)
                            : 0;

                    model.MonthlyGrossSalaries.Add(
                        new MonthlyGrossSalaryChartViewModel
                        {
                            Year = monthCursor.Year,
                            Month = monthCursor.Month,
                            MonthName = monthCursor.ToString(
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

            // Pending Final Pay List: only payroll rows with POST_FLAG = N.
            // The main dashboard connection was closed in the earlier finally block,
            // so reopen it here before querying pending payroll rows.
            bool closePendingConnection = connection.State != ConnectionState.Open;
            if (closePendingConnection)
                await connection.OpenAsync();

            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandTimeout = 120;
                    command.CommandText = @"
SELECT
    ISNULL(TRY_CONVERT(INT,t.[year]),0) AS PayrollYear,
    ISNULL(TRY_CONVERT(INT,t.[month]),0) AS PayrollMonth,
    ISNULL(TRY_CONVERT(INT,t.brnc),0) AS BranchCode,
    ISNULL(b.name, CONCAT('Branch ', ISNULL(CONVERT(VARCHAR(20),t.brnc),'Unknown'))) AS BranchName,
    COUNT(DISTINCT TRY_CONVERT(INT,t.code)) AS TotalEmployees,
    ISNULL(SUM(ISNULL(TRY_CONVERT(FLOAT,t.basic),0)),0) AS TotalBasic,
    ISNULL(SUM(ISNULL(TRY_CONVERT(FLOAT,t.tot_allw),0)),0) AS TotalAllowance,
    ISNULL(SUM(ISNULL(TRY_CONVERT(FLOAT,t.tot_dedu),0)),0) AS TotalDeduction,
    ISNULL(SUM(ISNULL(TRY_CONVERT(FLOAT,t.basic),0) +
               ISNULL(TRY_CONVERT(FLOAT,t.tot_allw),0) -
               ISNULL(TRY_CONVERT(FLOAT,t.tot_dedu),0)),0) AS NetSalary
FROM dbo.paytrn t
LEFT JOIN dbo.brncmast b
  ON TRY_CONVERT(INT,b.code) = TRY_CONVERT(INT,t.brnc)
WHERE UPPER(LTRIM(RTRIM(ISNULL(t.POST_FLAG,'')))) = 'N'
GROUP BY TRY_CONVERT(INT,t.[year]), TRY_CONVERT(INT,t.[month]),
         TRY_CONVERT(INT,t.brnc), b.name
ORDER BY TRY_CONVERT(INT,t.[year]) DESC,
         TRY_CONVERT(INT,t.[month]) DESC, b.name;";

                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        model.PendingFinalPayLists.Add(new PendingFinalPayListViewModel
                        {
                            Year = GetInt(reader, "PayrollYear"),
                            Month = GetInt(reader, "PayrollMonth"),
                            BranchCode = GetInt(reader, "BranchCode"),
                            BranchName = Convert.ToString(reader["BranchName"])?.Trim() ?? "",
                            TotalEmployees = GetInt(reader, "TotalEmployees"),
                            TotalBasic = GetDouble(reader, "TotalBasic"),
                            TotalAllowance = GetDouble(reader, "TotalAllowance"),
                            TotalDeduction = GetDouble(reader, "TotalDeduction"),
                            NetSalary = GetDouble(reader, "NetSalary")
                        });
                    }
                }
            }
            finally
            {
                if (closePendingConnection && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePendingFinalPayList(int year, int month, int branchCode)
        {
            if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("LoginId")))
                return RedirectToAction("Login", "Account");

            if (year < 1900 || year > 9999 || month < 1 || month > 12 || branchCode < 0)
            {
                TempData["PendingPayMessage"] = "Invalid year, month, or branch.";
                TempData["PendingPaySuccess"] = false;
                return RedirectToAction(nameof(Index));
            }

            var connection = _context.Database.GetDbConnection();
            bool closeConnection = connection.State != ConnectionState.Open;
            try
            {
                if (closeConnection) await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = @"
UPDATE dbo.paytrn
SET POST_FLAG = 'Y'
WHERE UPPER(LTRIM(RTRIM(ISNULL(POST_FLAG,'')))) = 'N'
  AND TRY_CONVERT(INT,[year]) = @Year
  AND TRY_CONVERT(INT,[month]) = @Month
  AND TRY_CONVERT(INT,brnc) = @BranchCode;";
                AddParameter(command, "@Year", year);
                AddParameter(command, "@Month", month);
                AddParameter(command, "@BranchCode", branchCode);
                int updatedRows = await command.ExecuteNonQueryAsync();

                TempData["PendingPaySuccess"] = updatedRows > 0;
                TempData["PendingPayMessage"] = updatedRows > 0
                    ? $"Final Pay List saved successfully. {updatedRows} payroll record(s) finalized."
                    : "No pending payroll records were found for the selected month and branch.";
            }
            catch (Exception)
            {
                TempData["PendingPaySuccess"] = false;
                TempData["PendingPayMessage"] = "Unable to save Final Pay List. Please check the database connection and dbo.paytrn columns.";
            }
            finally
            {
                if (closeConnection && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }

            return RedirectToAction(nameof(Index));
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
