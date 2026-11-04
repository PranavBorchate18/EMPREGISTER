
using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace Employee_Management_System.Controllers
{
    public class EmployeeTransactionDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmployeeTransactionDashboardController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            int? employeeCode,
            DateTime? fromDate,
            DateTime? toDate)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            var model = new EmployeeTransactionDashboardViewModel
            {
                EmployeeCode = employeeCode,
                FromDate = fromDate,
                ToDate = toDate
            };

            if (!employeeCode.HasValue)
                return View(model);

            if (employeeCode.Value <= 0)
            {
                model.ErrorMessage =
                    "Please enter a valid Employee Code.";

                return View(model);
            }

            if (!fromDate.HasValue || !toDate.HasValue)
            {
                model.ErrorMessage =
                    "Please select From Date and To Date.";

                return View(model);
            }

            if (fromDate.Value.Date > toDate.Value.Date)
            {
                model.ErrorMessage =
                    "From Date cannot be greater than To Date.";

                return View(model);
            }

            DbConnection cn =
                _context.Database.GetDbConnection();

            bool close = cn.State != ConnectionState.Open;

            try
            {
                if (close)
                    await cn.OpenAsync();

                // =========================================
                // EMPLOYEE INFORMATION
                // =========================================

                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT
                            p.code,
                            ISNULL(p.name,'') EmployeeName,
                            p.type,
                            ISNULL(g.name,'') GradeName,
                            ISNULL(b.name,'') BranchName,
                            p.join_date,
                            p.basic

                        FROM dbo.paymast p

                        LEFT JOIN dbo.gradmast g
                            ON g.code = p.grad_Code

                        LEFT JOIN dbo.brncmast b
                            ON b.code = p.brnc_code

                        WHERE p.code = @EmpCode";

                    Add(cmd, "@EmpCode", employeeCode.Value);

                    using var rd =
                        await cmd.ExecuteReaderAsync();

                    if (!await rd.ReadAsync())
                    {
                        model.ErrorMessage = "Employee not found.";
                        return View(model);
                    }

                    model.EmployeeFound = true;

                    model.EmployeeName =
                        Text(rd["EmployeeName"]);

                    model.EmployeeType =
                        rd["type"] == DBNull.Value
                            ? null
                            : Convert.ToInt32(rd["type"]);

                    model.GradeName =
                        Text(rd["GradeName"]);

                    model.BranchName =
                        Text(rd["BranchName"]);

                    model.JoiningDate =
                        rd["join_date"] == DBNull.Value
                            ? null
                            : Convert.ToDateTime(rd["join_date"]);

                    model.BasicSalary =
                        rd["basic"] == DBNull.Value
                            ? null
                            : Convert.ToDouble(rd["basic"]);
                }

                DateTime from = fromDate.Value.Date;

                DateTime to =
                    toDate.Value.Date.AddDays(1);

                // =========================================
                // 1. SALARY HISTORY
                // =========================================

                model.MonthlySalary = await Rows(
                    cn,
                    @"
                    SELECT
                        @EmpCode [Employee Code],

                        COUNT(*) [Total Months],

                        ISNULL(SUM(basic), 0) [Basic],

                        ISNULL(SUM(da), 0) [DA],

                        ISNULL(SUM(hra), 0) [HRA],

                        ISNULL(SUM(tot_allw), 0)
                            [Total Allowance],

                        ISNULL(SUM(tot_dedu), 0)
                            [Total Deduction],

                        ISNULL(
                            SUM(
                                ISNULL(basic, 0)
                                + ISNULL(tot_allw, 0)
                                - ISNULL(tot_dedu, 0)
                            ), 0
                        ) [Net Salary]

                    FROM dbo.paytrn

                    WHERE
                        code = @EmpCode

                        AND DATEFROMPARTS(
                            [year],
                            [month],
                            1
                        ) < @ToDate

                        AND EOMONTH(
                            DATEFROMPARTS(
                                [year],
                                [month],
                                1
                            )
                        ) >= @FromDate",

                    employeeCode.Value,
                    from,
                    to
                );

                // =========================================
                // 2. LEAVE BALANCE
                // =========================================

                // c_d_code = 1 : Credit
                // c_d_code = 2 : Debit

                model.LeaveCreditDebit = await Rows(
                    cn,
                    @"
                    SELECT
                        lt.sact_code [Leave Code],

                        ISNULL(lm.name, '') [Leave Name],

                        ISNULL(
                            SUM(
                                CASE
                                    WHEN lt.c_d_code = 2
                                    THEN ISNULL(lt.Num_of_days, 0)
                                    ELSE 0
                                END
                            ), 0
                        ) [Debit Days],

                        ISNULL(
                            SUM(
                                CASE
                                    WHEN lt.c_d_code = 1
                                        THEN ISNULL(
                                            lt.Num_of_days, 0
                                        )

                                    WHEN lt.c_d_code = 2
                                        THEN -ISNULL(
                                            lt.Num_of_days, 0
                                        )

                                    ELSE 0
                                END
                            ), 0
                        ) [Balance Days]

                    FROM dbo.leavtrn lt

                    LEFT JOIN dbo.leavmast lm
                        ON lm.code = lt.sact_code

                    WHERE
                        lt.empl = @EmpCode

                        AND lt.c_d_code IN (1, 2)

                        AND COALESCE(
                            lt.effect_pay_date,
                            lt.sact_from_date
                        ) < DATEADD(
                            DAY,
                            1,
                            CAST(GETDATE() AS DATE)
                        )

                    GROUP BY
                        lt.sact_code,
                        lm.name

                    ORDER BY
                        lm.name",

                    employeeCode.Value,
                    from,
                    to
                );

                // =========================================
                // 3. EMPLOYEE TYPE CHANGE HISTORY
                // =========================================

                model.EmployeeTypeHistory = await Rows(
                    cn,
                    @"
                    SELECT
                        SrNum [Serial No],

                        EmpCode [Employee Code],

                        Effect_Date [Effective Date],

                        CASE
                            WHEN Previous_Type IN (1, 2, 3, 4, 5)
                                THEN 'Permanent'

                            WHEN Previous_Type = 6
                                THEN 'Contract'

                            WHEN Previous_Type = 7
                                THEN 'Temporary'

                            WHEN Previous_Type = 8
                                THEN 'Retirement'

                            WHEN Previous_Type = 9
                                THEN 'Suspend'

                            WHEN Previous_Type = 10
                                THEN 'Resign'

                            ELSE 'Unknown'
                        END [Previous Type],

                        CASE
                            WHEN Change_Type IN (1, 2, 3, 4, 5)
                                THEN 'Permanent'

                            WHEN Change_Type = 6
                                THEN 'Contract'

                            WHEN Change_Type = 7
                                THEN 'Temporary'

                            WHEN Change_Type = 8
                                THEN 'Retirement'

                            WHEN Change_Type = 9
                                THEN 'Suspend'

                            WHEN Change_Type = 10
                                THEN 'Resign'

                            ELSE 'Unknown'
                        END [New Type],

                        [Operator],

                        Supervisor

                    FROM dbo.EmployeeTypeChange

                    WHERE
                        EmpCode = @EmpCode

                        AND Effect_Date >= @FromDate

                        AND Effect_Date < @ToDate

                    ORDER BY
                        Effect_Date,
                        SrNum",

                    employeeCode.Value,
                    from,
                    to
                );
            }
            catch (Exception ex)
            {
                model.ErrorMessage =
                    "Unable to load dashboard: " + ex.Message;
            }
            finally
            {
                if (close &&
                    cn.State == ConnectionState.Open)
                {
                    await cn.CloseAsync();
                }
            }

            return View(model);
        }

        // =========================================
        // LOGIN CHECK
        // =========================================

        private bool IsLoggedIn()
        {
            return !string.IsNullOrWhiteSpace(
                HttpContext.Session.GetString("LoginId")
            );
        }

        // =========================================
        // DATABASE VALUE CONVERSION
        // =========================================

        private static string Text(object value)
        {
            return value == DBNull.Value
                ? ""
                : Convert.ToString(value)?.Trim() ?? "";
        }

        // =========================================
        // SQL PARAMETER
        // =========================================

        private static void Add(
            DbCommand cmd,
            string name,
            object value)
        {
            var p = cmd.CreateParameter();

            p.ParameterName = name;
            p.Value = value;

            cmd.Parameters.Add(p);
        }

        // =========================================
        // FETCH TRANSACTION ROWS
        // =========================================

        private static async Task<List<Dictionary<string, string>>> Rows(
            DbConnection cn,
            string sql,
            int emp,
            DateTime from,
            DateTime to)
        {
            var result =
                new List<Dictionary<string, string>>();

            using var cmd = cn.CreateCommand();

            cmd.CommandText = sql;

            Add(cmd, "@EmpCode", emp);
            Add(cmd, "@FromDate", from);
            Add(cmd, "@ToDate", to);

            using var rd =
                await cmd.ExecuteReaderAsync();

            while (await rd.ReadAsync())
            {
                var row =
                    new Dictionary<string, string>();

                for (int i = 0; i < rd.FieldCount; i++)
                {
                    object val = rd.GetValue(i);

                    row[rd.GetName(i)] =
                        val == DBNull.Value
                            ? "-"
                            : val is DateTime dt
                                ? dt.ToString("dd-MM-yyyy")
                                : Convert.ToString(val) ?? "-";
                }

                result.Add(row);
            }

            return result;
        }
    }
}
