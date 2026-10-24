
using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace Employee_Management_System.Controllers
{
    public class LeaveBalanceReportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LeaveBalanceReportController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // INDEX - LEAVE BALANCE REPORT
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            DateTime? selectedDate,
            short? selectedLeaveCode)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new LeaveBalanceReportViewModel
            {
                SelectedDate = selectedDate ?? DateTime.Today,
                SelectedLeaveCode = selectedLeaveCode,
                LeaveTypes = await LoadLeaveTypes()
            };

            if (selectedLeaveCode.HasValue &&
                selectedLeaveCode.Value > 0)
            {
                bool validLeaveType = model.LeaveTypes.Any(
                    x => x.Value == selectedLeaveCode.Value.ToString());

                if (validLeaveType)
                {
                    model.Records = await LoadReport(
                        model.SelectedDate,
                        selectedLeaveCode.Value);
                }
                else
                {
                    model.SelectedLeaveCode = null;

                    ViewBag.Message =
                        "Selected Leave Type does not have a Leave Limit.";
                }
            }

            return View(model);
        }

        // =========================================================
        // LOAD LEAVE TYPES
        // Only Leave Types having leave_limit > 0
        // TABLE: dbo.leavmast
        // =========================================================
        private async Task<List<SelectListItem>> LoadLeaveTypes()
        {
            var result = new List<SelectListItem>();

            DbConnection connection =
                _context.Database.GetDbConnection();

            bool shouldClose =
                connection.State != ConnectionState.Open;

            try
            {
                if (shouldClose)
                {
                    await connection.OpenAsync();
                }

                using DbCommand command = connection.CreateCommand();

                command.CommandText = @"
SELECT
    code,
    ISNULL(name, '') AS LeaveName
FROM dbo.leavmast
WHERE
    ISNULL(Active, 'N') = 'Y'
    AND ISNULL(leave_limit, 0) > 0
ORDER BY code;
";

                using DbDataReader reader =
                    await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    short code = Convert.ToInt16(reader["code"]);

                    string name =
                        Convert.ToString(reader["LeaveName"])?.Trim() ?? "";

                    result.Add(new SelectListItem
                    {
                        Value = code.ToString(),
                        Text = code + " - " + name
                    });
                }
            }
            finally
            {
                if (shouldClose &&
                    connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }

            return result;
        }

        // =========================================================
        // LOAD LEAVE BALANCE REPORT
        //
        // Assumptions based on LeaveCreditController:
        // c_d_code = 1 : Leave Credit
        // Status = A  : Approved/active entry
        //
        // Other c_d_code values are treated as leave taken.
        // Confirm this rule against your Leave Debit/Statement logic.
        // =========================================================
        private async Task<List<LeaveBalanceReportRowViewModel>>
            LoadReport(DateTime selectedDate, short leaveCode)
        {
            var result =
                new List<LeaveBalanceReportRowViewModel>();

            DbConnection connection =
                _context.Database.GetDbConnection();

            bool shouldClose =
                connection.State != ConnectionState.Open;

            try
            {
                if (shouldClose)
                {
                    await connection.OpenAsync();
                }

                using DbCommand command = connection.CreateCommand();

                command.CommandText = @"
SELECT
    p.code AS EmployeeCode,
    ISNULL(p.name, '') AS EmployeeName,
    ISNULL(lm.name, '') AS LeaveName,
    CONVERT(DECIMAL(18, 2), ISNULL(lm.leave_limit, 0))
        AS LeaveLimit,

    CONVERT(DECIMAL(18, 2),
        ISNULL(credit.TotalCredit, 0))
        AS TotalCredit,

    CONVERT(DECIMAL(18, 2),
        ISNULL(taken.TotalTaken, 0))
        AS TotalTaken

FROM dbo.PayMast p

CROSS JOIN dbo.leavmast lm

LEFT JOIN
(
    SELECT
        l.empl,
        l.sact_code AS LeaveCode,
        SUM(ISNULL(l.Num_of_days, 0)) AS TotalCredit
    FROM dbo.leavtrn l
    WHERE
        l.c_d_code = 1
        AND UPPER(LTRIM(RTRIM(ISNULL(l.Status, '')))) = 'A'
        AND l.sact_from_date <= @SelectedDate
        AND l.sact_code = @LeaveCode
    GROUP BY
        l.empl,
        l.sact_code
) credit
    ON credit.empl = p.code
    AND credit.LeaveCode = lm.code

LEFT JOIN
(
    SELECT
        l.empl,
        l.dmnd_code AS LeaveCode,
        SUM(ISNULL(l.Num_of_days, 0)) AS TotalTaken
    FROM dbo.leavtrn l
    WHERE
        l.c_d_code <> 1
        AND UPPER(LTRIM(RTRIM(ISNULL(l.Status, '')))) = 'A'
        AND l.dmnd_from_date <= @SelectedDate
        AND l.dmnd_code = @LeaveCode
    GROUP BY
        l.empl,
        l.dmnd_code
) taken
    ON taken.empl = p.code
    AND taken.LeaveCode = lm.code

WHERE
    lm.code = @LeaveCode
    AND ISNULL(lm.Active, 'N') = 'Y'
    AND ISNULL(lm.leave_limit, 0) > 0

ORDER BY
    p.code;
";

                AddParameter(command, "@SelectedDate", selectedDate.Date);
                AddParameter(command, "@LeaveCode", leaveCode);

                using DbDataReader reader =
                    await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    decimal leaveLimit =
                        Convert.ToDecimal(reader["LeaveLimit"]);

                    decimal totalCredit =
                        Convert.ToDecimal(reader["TotalCredit"]);

                    decimal totalTaken =
                        Convert.ToDecimal(reader["TotalTaken"]);

                    // Balance = credit - taken.
                    decimal leaveBalance =
                        totalCredit - totalTaken;

                    result.Add(new LeaveBalanceReportRowViewModel
                    {
                        EmployeeCode =
                            Convert.ToString(reader["EmployeeCode"]) ?? "",

                        EmployeeName =
                            Convert.ToString(reader["EmployeeName"])?.Trim() ?? "",

                        LeaveType =
                            Convert.ToString(reader["LeaveName"])?.Trim() ?? "",

                        LeaveLimit = leaveLimit,
                        LeaveTaken = totalTaken,
                        LeaveBalance = leaveBalance
                    });
                }
            }
            finally
            {
                if (shouldClose &&
                    connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }

            return result;
        }

        // =========================================================
        // LOGIN CHECK
        // =========================================================
        private bool IsLoggedIn()
        {
            return !string.IsNullOrWhiteSpace(
                HttpContext.Session.GetString("LoginId"));
        }

        // =========================================================
        // COMMON SQL PARAMETER
        // =========================================================
        private static void AddParameter(
            DbCommand command,
            string parameterName,
            object? value)
        {
            DbParameter parameter = command.CreateParameter();

            parameter.ParameterName = parameterName;
            parameter.Value = value ?? DBNull.Value;

            command.Parameters.Add(parameter);
        }
    }
}
