using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;

namespace Employee_Management_System.Controllers
{
    public class SalarySlipController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SalarySlipController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // SALARY SLIP PAGE
        // ==========================================
        [HttpGet]
        public IActionResult Index()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            var model = new SalarySlipViewModel
            {
                Year = DateTime.Now.Year,
                Month = DateTime.Now.Month
            };

            return View(model);
        }

        // ==========================================
        // GENERATE SALARY SLIP
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(
            int year,
            int month,
            int employeeCode)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            var model = new SalarySlipViewModel
            {
                Year = year,
                Month = month,
                EmployeeCode = employeeCode
            };

            // ------------------------------------------
            // VALIDATION
            // ------------------------------------------
            if (year < 1900)
            {
                model.Message = "Please select a valid Year.";
                return View(model);
            }

            if (month < 1 || month > 12)
            {
                model.Message = "Please select a valid Month.";
                return View(model);
            }

            if (employeeCode <= 0)
            {
                model.Message = "Please enter a valid Employee Code.";
                return View(model);
            }

            var connection = _context.Database.GetDbConnection();

            bool closeConnection =
                connection.State != ConnectionState.Open;

            try
            {
                if (closeConnection)
                    await connection.OpenAsync();

                // ==========================================
                // STEP 1 - EMPLOYEE INFORMATION
                // PayMast + brncmast + gradmast
                // ==========================================
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT TOP 1
       p.code AS EmployeeCode,
       ISNULL(p.name,'') AS EmployeeName,

       p.join_date AS JoiningDate,
       p.Retire_Date AS RetirementDate,
       p.birth_date AS BirthDate,

       ISNULL(p.pf_no,'') AS PFNumber,
       ISNULL(p.uan_no,'') AS UANNumber,
       ISNULL(p.Pan_No,'') AS PANNumber,

       ISNULL(
           CONVERT(NVARCHAR(50), p.Aadhar_No),
           ''
       ) AS AadhaarNumber,

       ISNULL(b.name,'') AS BranchName,
       ISNULL(g.name,'') AS Designation

FROM dbo.PayMast p

LEFT JOIN dbo.brncmast b
       ON TRY_CONVERT(INT,b.code)
        = TRY_CONVERT(INT,p.brnc_code)

LEFT JOIN dbo.gradmast g
       ON TRY_CONVERT(INT,g.code)
        = TRY_CONVERT(INT,p.grad_Code)

WHERE TRY_CONVERT(INT,p.code)
      = @EmployeeCode;";

                    AddParameter(
                        command,
                        "@EmployeeCode",
                        employeeCode);

                    using var reader =
                        await command.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        model.Message =
                            $"Employee Code {employeeCode} not found.";

                        return View(model);
                    }

                    model.EmployeeCode =
                        Convert.ToInt32(
                            reader["EmployeeCode"]);

                    model.EmployeeName =
                        Convert.ToString(
                            reader["EmployeeName"])?.Trim() ?? "";

                    model.Designation =
                        Convert.ToString(
                            reader["Designation"])?.Trim() ?? "";

                    model.BranchName =
                        Convert.ToString(
                            reader["BranchName"])?.Trim() ?? "";

                    model.JoiningDate =
                        NullableDate(reader["JoiningDate"]);

                    model.RetirementDate =
                        NullableDate(reader["RetirementDate"]);

                    model.BirthDate =
                        NullableDate(reader["BirthDate"]);

                    model.PFNumber =
                        Convert.ToString(
                            reader["PFNumber"])?.Trim() ?? "";

                    model.UANNumber =
                        Convert.ToString(
                            reader["UANNumber"])?.Trim() ?? "";

                    model.PANNumber =
                        Convert.ToString(
                            reader["PANNumber"])?.Trim() ?? "";

                    model.AadhaarNumber =
                        Convert.ToString(
                            reader["AadhaarNumber"])?.Trim() ?? "";
                }

                // ==========================================
                // STEP 2 - SALARY SUMMARY
                // dbo.paytrn
                // ==========================================
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT TOP 1

       ISNULL(
           TRY_CONVERT(FLOAT,basic),
           0
       ) AS BasicSalary,

       ISNULL(
           TRY_CONVERT(FLOAT,da),
           0
       ) AS DA,

       ISNULL(
           TRY_CONVERT(FLOAT,hra),
           0
       ) AS HRA,

       ISNULL(
           TRY_CONVERT(FLOAT,tot_allw),
           0
       ) AS TotalAllowance,

       ISNULL(
           TRY_CONVERT(FLOAT,tot_dedu),
           0
       ) AS TotalDeduction,

       ISNULL(
           TRY_CONVERT(FLOAT,days),
           0
       ) AS PayableDays

FROM dbo.paytrn

WHERE TRY_CONVERT(INT,[year])
      = @Year

AND TRY_CONVERT(INT,[month])
    = @Month

AND TRY_CONVERT(INT,code)
    = @EmployeeCode

ORDER BY issue_date DESC;";

                    AddParameter(
                        command,
                        "@Year",
                        year);

                    AddParameter(
                        command,
                        "@Month",
                        month);

                    AddParameter(
                        command,
                        "@EmployeeCode",
                        employeeCode);

                    using var reader =
                        await command.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        model.Message =
                            $"Salary data not found for Employee Code {employeeCode} for {GetMonthName(month)} {year}. Please complete Pay Calculation first.";

                        return View(model);
                    }

                    model.BasicSalary =
                        Number(reader["BasicSalary"]);

                    model.DA =
                        Number(reader["DA"]);

                    model.HRA =
                        Number(reader["HRA"]);

                    model.TotalAllowance =
                        Number(reader["TotalAllowance"]);

                    model.TotalDeduction =
                        Number(reader["TotalDeduction"]);

                    model.PayableDays =
                        Number(reader["PayableDays"]);
                }

                // ==========================================
                // STEP 3 - EARNINGS
                // paytrnft flag = 1
                // + allwmast
                // ==========================================
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT
       TRY_CONVERT(
           INT,
           ft.allw_dedu_code
       ) AS ComponentCode,

       LTRIM(
           RTRIM(
               ISNULL(am.name,'')
           )
       ) AS ComponentName,

       SUM(
           ISNULL(
               TRY_CONVERT(FLOAT,ft.amt),
               0
           )
       ) AS Amount

FROM dbo.paytrnft ft

INNER JOIN dbo.allwmast am
        ON TRY_CONVERT(INT,am.code)
         = TRY_CONVERT(
             INT,
             ft.allw_dedu_code
           )

WHERE TRY_CONVERT(INT,ft.[year])
      = @Year

AND TRY_CONVERT(INT,ft.[month])
    = @Month

AND TRY_CONVERT(INT,ft.empl)
    = @EmployeeCode

AND TRY_CONVERT(INT,ft.flag)
    = 1

GROUP BY
       ft.allw_dedu_code,
       am.name

ORDER BY
       TRY_CONVERT(
           INT,
           ft.allw_dedu_code
       );";

                    AddParameter(
                        command,
                        "@Year",
                        year);

                    AddParameter(
                        command,
                        "@Month",
                        month);

                    AddParameter(
                        command,
                        "@EmployeeCode",
                        employeeCode);

                    using var reader =
                        await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        model.Earnings.Add(
                            new SalarySlipComponentViewModel
                            {
                                Code =
                                    reader["ComponentCode"]
                                        == DBNull.Value
                                    ? 0
                                    : Convert.ToInt32(
                                        reader["ComponentCode"]),

                                Name =
                                    Convert.ToString(
                                        reader["ComponentName"])
                                        ?.Trim() ?? "",

                                Amount =
                                    Math.Round(
                                        Number(
                                            reader["Amount"]),
                                        2)
                            });
                    }
                }

                // ==========================================
                // STEP 4 - DEDUCTIONS
                // paytrnft flag = 2
                // + dedumast
                // ==========================================
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT
       TRY_CONVERT(
           INT,
           ft.allw_dedu_code
       ) AS ComponentCode,

       LTRIM(
           RTRIM(
               ISNULL(dm.name,'')
           )
       ) AS ComponentName,

       SUM(
           ISNULL(
               TRY_CONVERT(FLOAT,ft.amt),
               0
           )
       ) AS Amount

FROM dbo.paytrnft ft

INNER JOIN dbo.dedumast dm
        ON TRY_CONVERT(INT,dm.code)
         = TRY_CONVERT(
             INT,
             ft.allw_dedu_code
           )

WHERE TRY_CONVERT(INT,ft.[year])
      = @Year

AND TRY_CONVERT(INT,ft.[month])
    = @Month

AND TRY_CONVERT(INT,ft.empl)
    = @EmployeeCode

AND TRY_CONVERT(INT,ft.flag)
    = 2

GROUP BY
       ft.allw_dedu_code,
       dm.name

ORDER BY
       TRY_CONVERT(
           INT,
           ft.allw_dedu_code
       );";

                    AddParameter(
                        command,
                        "@Year",
                        year);

                    AddParameter(
                        command,
                        "@Month",
                        month);

                    AddParameter(
                        command,
                        "@EmployeeCode",
                        employeeCode);

                    using var reader =
                        await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        model.Deductions.Add(
                            new SalarySlipComponentViewModel
                            {
                                Code =
                                    reader["ComponentCode"]
                                        == DBNull.Value
                                    ? 0
                                    : Convert.ToInt32(
                                        reader["ComponentCode"]),

                                Name =
                                    Convert.ToString(
                                        reader["ComponentName"])
                                        ?.Trim() ?? "",

                                Amount =
                                    Math.Round(
                                        Number(
                                            reader["Amount"]),
                                        2)
                            });
                    }
                }

                // ==========================================
                // STEP 5 - WITHOUT PAY LEAVE (WPL)
                // leavtrn: sact_code = 10, Status = A
                // Reduce Basic day-wise for selected month.
                // ==========================================
                double withoutPayLeaveDays = 0;

                using (var command = connection.CreateCommand())
                {
                    var monthStart = new DateTime(year, month, 1);
                    var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                    command.CommandText = @"
SELECT ISNULL(SUM(
    CASE
        WHEN sact_from_date IS NULL OR sact_to_date IS NULL THEN 0
        ELSE DATEDIFF(
            DAY,
            CASE WHEN sact_from_date < @MonthStart THEN @MonthStart ELSE sact_from_date END,
            CASE WHEN sact_to_date > @MonthEnd THEN @MonthEnd ELSE sact_to_date END
        ) + 1
    END
), 0)
FROM dbo.leavtrn
WHERE TRY_CONVERT(INT, empl) = @EmployeeCode
  AND TRY_CONVERT(INT, sact_code) = 10
  AND UPPER(LTRIM(RTRIM(ISNULL(Status,'')))) = 'A'
  AND sact_from_date <= @MonthEnd
  AND sact_to_date >= @MonthStart;";

                    AddParameter(command, "@EmployeeCode", employeeCode);
                    AddParameter(command, "@MonthStart", monthStart);
                    AddParameter(command, "@MonthEnd", monthEnd);

                    var result = await command.ExecuteScalarAsync();
                    withoutPayLeaveDays = Number(result ?? 0);
                }

                // paytrn.basic is treated as the original monthly Basic.
                // Example: Basic 3750 / 30 days * 4 WPL = 500 reduction => 3250.
                int totalDaysInMonth = DateTime.DaysInMonth(year, month);
                double originalBasic = model.BasicSalary;

                if (withoutPayLeaveDays > 0 && originalBasic > 0 && totalDaysInMonth > 0)
                {
                    // Never deduct more days than the selected month contains.
                    withoutPayLeaveDays = Math.Min(withoutPayLeaveDays, totalDaysInMonth);

                    double perDayBasic = originalBasic / totalDaysInMonth;
                    double wplBasicReduction = perDayBasic * withoutPayLeaveDays;

                    model.BasicSalary = Math.Round(
                        Math.Max(0, originalBasic - wplBasicReduction),
                        2);

                    model.PayableDays = Math.Max(0, totalDaysInMonth - withoutPayLeaveDays);

                    // Keep Basic earning in sync when a Basic row exists in paytrnft.
                    var basicEarning = model.Earnings.FirstOrDefault(x =>
                        x.Name.Equals("BASIC", StringComparison.OrdinalIgnoreCase));

                    if (basicEarning != null)
                        basicEarning.Amount = model.BasicSalary;
                }

                // ==========================================
                // STEP 6 - FINAL CALCULATION
                // ==========================================

                model.GrossSalary =
                    Math.Round(
                        model.BasicSalary
                        + model.TotalAllowance,
                        2);

                model.NetSalary =
                    Math.Round(
                        model.GrossSalary
                        - model.TotalDeduction,
                        2);

                model.MonthName =
                    GetMonthName(month);

                model.NetSalaryInWords =
                    NumberToWords(
                        Convert.ToInt64(
                            Math.Round(
                                model.NetSalary,
                                0)));

                model.IsGenerated = true;

                return View(model);
            }
            catch (Exception ex)
            {
                model.Message =
                    "Unable to generate Salary Slip. "
                    + ex.Message;

                return View(model);
            }
            finally
            {
                if (closeConnection &&
                    connection.State
                    == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }

        // ==========================================
        // MONTH NAME
        // ==========================================
        private static string GetMonthName(int month)
        {
            if (month < 1 || month > 12)
                return "";

            return CultureInfo
                .CurrentCulture
                .DateTimeFormat
                .GetMonthName(month);
        }

        // ==========================================
        // NUMBER TO WORDS
        // ==========================================
        private static string NumberToWords(long number)
        {
            if (number == 0)
                return "Zero Only";

            if (number < 0)
                return "Minus "
                       + NumberToWords(
                           Math.Abs(number));

            string words = "";

            if ((number / 10000000) > 0)
            {
                words += NumberToWordsWithoutOnly(
                    number / 10000000)
                    + " Crore ";

                number %= 10000000;
            }

            if ((number / 100000) > 0)
            {
                words += NumberToWordsWithoutOnly(
                    number / 100000)
                    + " Lakh ";

                number %= 100000;
            }

            if ((number / 1000) > 0)
            {
                words += NumberToWordsWithoutOnly(
                    number / 1000)
                    + " Thousand ";

                number %= 1000;
            }

            if ((number / 100) > 0)
            {
                words += NumberToWordsWithoutOnly(
                    number / 100)
                    + " Hundred ";

                number %= 100;
            }

            if (number > 0)
            {
                if (words != "")
                    words += "";

                var unitsMap =
                    new[]
                    {
                        "Zero",
                        "One",
                        "Two",
                        "Three",
                        "Four",
                        "Five",
                        "Six",
                        "Seven",
                        "Eight",
                        "Nine",
                        "Ten",
                        "Eleven",
                        "Twelve",
                        "Thirteen",
                        "Fourteen",
                        "Fifteen",
                        "Sixteen",
                        "Seventeen",
                        "Eighteen",
                        "Nineteen"
                    };

                var tensMap =
                    new[]
                    {
                        "Zero",
                        "Ten",
                        "Twenty",
                        "Thirty",
                        "Forty",
                        "Fifty",
                        "Sixty",
                        "Seventy",
                        "Eighty",
                        "Ninety"
                    };

                if (number < 20)
                {
                    words +=
                        unitsMap[number];
                }
                else
                {
                    words +=
                        tensMap[number / 10];

                    if ((number % 10) > 0)
                    {
                        words += "-"
                                 + unitsMap[
                                     number % 10];
                    }
                }
            }

            return words.Trim() + " Only";
        }

        private static string NumberToWordsWithoutOnly(
            long number)
        {
            string result =
                NumberToWords(number);

            if (result.EndsWith(" Only"))
            {
                result =
                    result.Substring(
                        0,
                        result.Length - 5);
            }

            return result.Trim();
        }

        // ==========================================
        // SESSION
        // ==========================================
        private bool IsLoggedIn()
        {
            return !string.IsNullOrWhiteSpace(
                HttpContext.Session
                    .GetString("LoginId"));
        }

        // ==========================================
        // DB HELPERS
        // ==========================================
        private static void AddParameter(
            DbCommand command,
            string name,
            object? value)
        {
            var parameter =
                command.CreateParameter();

            parameter.ParameterName = name;

            parameter.Value =
                value ?? DBNull.Value;

            command.Parameters.Add(parameter);
        }

        private static double Number(object value)
        {
            return value == DBNull.Value
                ? 0
                : Convert.ToDouble(value);
        }

        private static DateTime? NullableDate(
            object value)
        {
            if (value == DBNull.Value)
                return null;

            return Convert.ToDateTime(value);
        }
    }
}