using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace Employee_Management_System.Controllers
{
    public class LeaveDebitController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LeaveDebitController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            return View(new LeaveDebitViewModel
            {
                SelectedYear = DateTime.Now.Year,
                LeaveTypes = await LoadLeaveTypes()
            });
        }

        private async Task<List<SelectListItem>> LoadLeaveTypes()
        {
            var result = new List<SelectListItem>();
            DbConnection connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;

            try
            {
                if (close) await connection.OpenAsync();

                using DbCommand command = connection.CreateCommand();
                command.CommandText = @"
SELECT code, name
FROM dbo.leavmast
WHERE UPPER(LTRIM(RTRIM(ISNULL(Active,'N')))) = 'Y'
  AND UPPER(LTRIM(RTRIM(ISNULL(store_yn,'')))) = 'N'
ORDER BY code;";

                using DbDataReader reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    short code = Convert.ToInt16(reader["code"]);
                    string name = reader["name"] == DBNull.Value
                        ? ""
                        : Convert.ToString(reader["name"])?.Trim() ?? "";

                    result.Add(new SelectListItem
                    {
                        Value = code.ToString(),
                        Text = code + " - " + name
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
        public async Task<IActionResult> Search(int year, short leaveCode)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, sessionExpired = true, message = "Login session expired." });

            if (year <= 0)
                return Json(new { success = false, message = "Please select Year." });

            if (leaveCode <= 0)
                return Json(new { success = false, message = "Please select Leave Type." });

            if (!await IsAllowedLeaveType(leaveCode))
                return Json(new { success = false, message = "Selected Leave Type is not available for Leave Debit." });

            DateTime fromDate = new(year, 1, 1);
            DateTime toDate = new(year + 1, 1, 1);
            var result = new List<LeaveDebitEmployeeViewModel>();

            DbConnection connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;

            try
            {
                if (close) await connection.OpenAsync();

                using DbCommand command = connection.CreateCommand();
                command.CommandText = @"
SELECT
    p.code AS EmployeeCode,
    ISNULL(p.name,'') AS EmployeeName,
    ISNULL(SUM(CASE
        WHEN l.c_d_code = 1
         AND UPPER(LTRIM(RTRIM(ISNULL(l.Status,'')))) = 'A'
        THEN ISNULL(l.Num_of_days,0) ELSE 0 END),0) AS LeaveCredit,
    ISNULL(SUM(CASE
        WHEN l.c_d_code = 2
         AND UPPER(LTRIM(RTRIM(ISNULL(l.Status,'')))) = 'A'
        THEN ISNULL(l.Num_of_days,0) ELSE 0 END),0) AS LeaveDebit
FROM dbo.PayMast p
INNER JOIN dbo.leavtrn l
    ON l.empl = p.code
   AND l.sact_code = @LeaveCode
   AND l.sact_from_date >= @FromDate
   AND l.sact_from_date < @ToDate
   AND l.c_d_code IN (1,2)
   AND UPPER(LTRIM(RTRIM(ISNULL(l.Status,'')))) = 'A'
GROUP BY p.code,p.name
ORDER BY p.code;";

                AddParameter(command, "@LeaveCode", leaveCode);
                AddParameter(command, "@FromDate", fromDate);
                AddParameter(command, "@ToDate", toDate);

                using DbDataReader reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    double credit = Convert.ToDouble(reader["LeaveCredit"]);
                    double debit = Convert.ToDouble(reader["LeaveDebit"]);

                    result.Add(new LeaveDebitEmployeeViewModel
                    {
                        EmployeeCode = Convert.ToInt32(reader["EmployeeCode"]),
                        EmployeeName = Convert.ToString(reader["EmployeeName"])?.Trim() ?? "",
                        LeaveCredit = credit,
                        LeaveDebit = debit,
                        LeaveBalance = credit - debit
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Unable to load employee leave balance. " + ex.Message
                });
            }
            finally
            {
                if (close && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }

            return Json(new
            {
                success = true,
                totalEmployees = result.Count,
                yearEndDate = new DateTime(year, 12, 31).ToString("dd-MM-yyyy"),
                data = result
            });
        }

        // =========================================================
        // LEAVE LAPSE
        // Inserts one approved debit row for every employee having
        // a positive balance for selected Year + Leave Type.
        // Num_of_days = current available balance.
        // c_d_code = 2, Status = A.
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LeaveLapse(int year, short leaveCode)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, sessionExpired = true, message = "Login session expired." });

            if (year <= 0 || leaveCode <= 0)
                return Json(new { success = false, message = "Please select valid Year and Leave Type." });

            if (!await IsAllowedLeaveType(leaveCode))
                return Json(new { success = false, message = "Selected Leave Type is not available for Leave Lapse." });

            DateTime fromDate = new(year, 1, 1);
            DateTime toDate = new(year + 1, 1, 1);
            DateTime lapseDate = new(year, 12, 31);

            string loginId = HttpContext.Session.GetString("LoginId")?.Trim() ?? "";
            int sanctionOfficer = 0;
            int.TryParse(loginId, out sanctionOfficer);

            DbConnection connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;
            DbTransaction? transaction = null;

            try
            {
                if (close) await connection.OpenAsync();
                transaction = await connection.BeginTransactionAsync();

                // IMPORTANT:
                // leavtrn has a composite key (sact_from_date, empl).
                // If an employee already has a transaction on 31-Dec,
                // use a unique time on the same date while keeping the
                // displayed lapse date as 31-12-yyyy.
                using DbCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
;WITH BalanceData AS
(
    SELECT
        p.code AS EmployeeCode,
        ISNULL(SUM(CASE
            WHEN l.c_d_code = 1
             AND UPPER(LTRIM(RTRIM(ISNULL(l.Status,'')))) = 'A'
            THEN ISNULL(l.Num_of_days,0) ELSE 0 END),0)
        -
        ISNULL(SUM(CASE
            WHEN l.c_d_code = 2
             AND UPPER(LTRIM(RTRIM(ISNULL(l.Status,'')))) = 'A'
            THEN ISNULL(l.Num_of_days,0) ELSE 0 END),0) AS BalanceDays
    FROM dbo.PayMast p
    INNER JOIN dbo.leavtrn l
        ON l.empl = p.code
       AND l.sact_code = @LeaveCode
       AND l.sact_from_date >= @FromDate
       AND l.sact_from_date < @ToDate
       AND l.c_d_code IN (1,2)
       AND UPPER(LTRIM(RTRIM(ISNULL(l.Status,'')))) = 'A'
    GROUP BY p.code
),
Eligible AS
(
    SELECT EmployeeCode, BalanceDays
    FROM BalanceData
    WHERE BalanceDays > 0
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.leavtrn x
          WHERE x.empl = BalanceData.EmployeeCode
            AND x.sact_code = @LeaveCode
            AND x.c_d_code = 2
            AND UPPER(LTRIM(RTRIM(ISNULL(x.Status,'')))) = 'A'
            AND CAST(x.sact_from_date AS date) = CAST(@LapseDate AS date)
            AND CAST(x.sact_to_date AS date) = CAST(@LapseDate AS date)
            AND ISNULL(x.reason_Remark,'') = 'LEAVE LAPSE'
      )
)
INSERT INTO dbo.leavtrn
(
    sact_from_date,
    empl,
    c_d_code,
    sact_to_date,
    sact_code,
    dmnd_from_date,
    dmnd_to_date,
    dmnd_code,
    prv_days,
    sact_officer,
    reason,
    reason_Remark,
    cherge_to,
    effect_pay_date,
    Leave_App_ID,
    Operator,
    Status,
    Num_of_days
)
SELECT
    DATEADD(
        SECOND,
        ROW_NUMBER() OVER (ORDER BY e.EmployeeCode) - 1,
        @LapseDate
    ),
    e.EmployeeCode,
    2,
    @LapseDate,
    @LeaveCode,
    @LapseDate,
    @LapseDate,
    @LeaveCode,
    e.BalanceDays,
    @SanctionOfficer,
    NULL,
    'LEAVE LAPSE',
    NULL,
    @LapseDate,
    NULL,
    @Operator,
    'A',
    e.BalanceDays
FROM Eligible e;";

                AddParameter(command, "@LeaveCode", leaveCode);
                AddParameter(command, "@FromDate", fromDate);
                AddParameter(command, "@ToDate", toDate);
                AddParameter(command, "@LapseDate", lapseDate);
                AddParameter(command, "@SanctionOfficer", sanctionOfficer);
                AddParameter(command, "@Operator", loginId);

                int inserted = await command.ExecuteNonQueryAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    inserted,
                    lapseDate = lapseDate.ToString("dd-MM-yyyy"),
                    message = inserted > 0
                        ? $"Leave Lapse completed successfully. {inserted} employee(s) inserted."
                        : "No employee with positive balance was available for Leave Lapse, or Leave Lapse was already completed."
                });
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    try { await transaction.RollbackAsync(); } catch { }
                }

                return Json(new
                {
                    success = false,
                    message = "Leave Lapse failed. " + ex.Message
                });
            }
            finally
            {
                if (transaction != null)
                    await transaction.DisposeAsync();

                if (close && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }

        private async Task<bool> IsAllowedLeaveType(short leaveCode)
        {
            DbConnection connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;

            try
            {
                if (close) await connection.OpenAsync();

                using DbCommand command = connection.CreateCommand();
                command.CommandText = @"
SELECT COUNT(1)
FROM dbo.leavmast
WHERE code=@LeaveCode
  AND UPPER(LTRIM(RTRIM(ISNULL(Active,'N'))))='Y'
  AND UPPER(LTRIM(RTRIM(ISNULL(store_yn,''))))='N';";

                AddParameter(command, "@LeaveCode", leaveCode);
                return Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0) > 0;
            }
            finally
            {
                if (close && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }

        private bool IsLoggedIn() =>
            !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("LoginId"));

        private static void AddParameter(DbCommand command, string name, object? value)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}
