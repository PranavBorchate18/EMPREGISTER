using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace Employee_Management_System.Controllers
{
    public class LeaveController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LeaveController(ApplicationDbContext context)
        {
            _context = context;
        }


        // =========================================================
        // INDEX
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = new LeaveApplicationViewModel();

            await LoadLeaveTypes(model);

            return View(model);
        }


        // =========================================================
        // GET EMPLOYEE
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetEmployee(int employeeCode)
        {
            try
            {
                if (employeeCode <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Please enter valid Employee Code."
                    });
                }

                var employee = await _context.PayMasts
                    .AsNoTracking()
                    .Where(x => x.EmployeeCode == employeeCode)
                    .Select(x => new
                    {
                        employeeCode = x.EmployeeCode,
                        employeeName = x.EmployeeName,
                        gender = x.Sex
                    })
                    .FirstOrDefaultAsync();

                if (employee == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Employee not found."
                    });
                }

                return Json(new
                {
                    success = true,
                    data = employee
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // =========================================================
        // SAVE LEAVE
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(
            LeaveApplicationViewModel model)
        {
            try
            {
                // =================================================
                // EMPLOYEE VALIDATION
                // =================================================
                if (model.EmployeeCode <= 0)
                {
                    TempData["Error"] =
                        "Please enter Employee Code.";

                    await LoadLeaveTypes(model);

                    return View("Index", model);
                }


                bool employeeExists = await _context.PayMasts
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.EmployeeCode == model.EmployeeCode);

                if (!employeeExists)
                {
                    TempData["Error"] =
                        "Employee not found.";

                    await LoadLeaveTypes(model);

                    return View("Index", model);
                }


                // =================================================
                // DEMAND FROM
                // =================================================
                if (!model.DemandFromDate.HasValue)
                {
                    TempData["Error"] =
                        "From Date is required.";

                    await LoadLeaveTypes(model);

                    return View("Index", model);
                }


                // =================================================
                // DEMAND TO
                // =================================================
                if (!model.DemandToDate.HasValue)
                {
                    TempData["Error"] =
                        "To Date is required.";

                    await LoadLeaveTypes(model);

                    return View("Index", model);
                }


                if (model.DemandToDate.Value.Date <
                    model.DemandFromDate.Value.Date)
                {
                    TempData["Error"] =
                        "To Date cannot be less than From Date.";

                    await LoadLeaveTypes(model);

                    return View("Index", model);
                }


                // =================================================
                // DEMAND LEAVE TYPE
                // =================================================
                if (!model.DemandLeaveCode.HasValue ||
                    model.DemandLeaveCode.Value <= 0)
                {
                    TempData["Error"] =
                        "Please select Leave Type.";

                    await LoadLeaveTypes(model);

                    return View("Index", model);
                }


                if (!await LeaveCodeExists(
                    model.DemandLeaveCode.Value))
                {
                    TempData["Error"] =
                        "Invalid Leave Type.";

                    await LoadLeaveTypes(model);

                    return View("Index", model);
                }


                // =================================================
                // SANCTION FROM
                // =================================================
                if (!model.SanctionFromDate.HasValue)
                {
                    TempData["Error"] =
                        "Sanction From Date is required.";

                    await LoadLeaveTypes(model);

                    return View("Index", model);
                }


                // =================================================
                // SANCTION LEAVE
                // =================================================
                if (!model.SanctionLeaveCode.HasValue ||
                    model.SanctionLeaveCode.Value <= 0)
                {
                    TempData["Error"] =
                        "Please select Sanction Leave Type.";

                    await LoadLeaveTypes(model);

                    return View("Index", model);
                }


                if (model.SanctionToDate.HasValue &&
                    model.SanctionToDate.Value.Date <
                    model.SanctionFromDate.Value.Date)
                {
                    TempData["Error"] =
                        "Sanction To Date cannot be less than Sanction From Date.";

                    await LoadLeaveTypes(model);

                    return View("Index", model);
                }


                if (!await LeaveCodeExists(
                    model.SanctionLeaveCode.Value))
                {
                    TempData["Error"] =
                        "Invalid Sanction Leave Type.";

                    await LoadLeaveTypes(model);

                    return View("Index", model);
                }


                // =================================================
                // MATERNITY LEAVE - FEMALE EMPLOYEE ONLY
                // =================================================
                bool demandIsMaternity =
                    await IsMaternityLeave(model.DemandLeaveCode.Value);

                bool sanctionIsMaternity =
                    await IsMaternityLeave(model.SanctionLeaveCode.Value);

                if (demandIsMaternity || sanctionIsMaternity)
                {
                    string? employeeGender =
                        await _context.PayMasts
                            .AsNoTracking()
                            .Where(x => x.EmployeeCode == model.EmployeeCode)
                            .Select(x => x.Sex)
                            .FirstOrDefaultAsync();

                    if (!IsFemale(employeeGender))
                    {
                        TempData["Error"] =
                            "Maternity Leave is only applicable for Female Employees.";

                        await LoadLeaveTypes(model);
                        return View("Index", model);
                    }
                }


                // =================================================
                // CHARGE TO VALIDATION
                // =================================================
                if (model.ChargeTo.HasValue &&
                    model.ChargeTo.Value > 0)
                {
                    bool chargeEmployeeExists =
                        await _context.PayMasts
                            .AsNoTracking()
                            .AnyAsync(x =>
                                x.EmployeeCode ==
                                model.ChargeTo.Value);

                    if (!chargeEmployeeExists)
                    {
                        TempData["Error"] =
                            "Charge To Transfer Employee not found.";

                        await LoadLeaveTypes(model);

                        return View("Index", model);
                    }
                }


                // =================================================
                // AUTOMATIC VALUES
                // =================================================

                // Employee Code -> sact_officer
                int sanctionOfficerCode =
                    model.EmployeeCode;


                // Logged-in User -> Operator
                // Keep LoginId as string so leading zero is preserved (example: 0119).
                string operatorCode =
                    HttpContext.Session.GetString("LoginId") ?? "";


                // =================================================
                // C/D CODE
                //
                // ALWAYS STORE 2
                // NOT TAKEN FROM DASHBOARD
                // =================================================
                short cdCode = 2;


                // =================================================
                // DATABASE CONNECTION
                // =================================================
                DbConnection connection =
                    _context.Database.GetDbConnection();

                bool shouldCloseConnection =
                    connection.State != ConnectionState.Open;

                if (shouldCloseConnection)
                {
                    await connection.OpenAsync();
                }


                decimal newLeaveApplicationId;


                try
                {
                    // =============================================
                    // AUTO LEAVE APPLICATION ID
                    // =============================================
                    using (DbCommand idCommand =
                           connection.CreateCommand())
                    {
                        idCommand.CommandText = @"
SELECT ISNULL(MAX(Leave_App_ID), 0) + 1
FROM dbo.leavtrn;";

                        object? result =
                            await idCommand.ExecuteScalarAsync();

                        newLeaveApplicationId =
                            Convert.ToDecimal(result ?? 1);
                    }


                    // =============================================
                    // INSERT LEAVE
                    // =============================================
                    using DbCommand command =
                        connection.CreateCommand();

                    command.CommandText = @"
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
    Status
)
VALUES
(
    @sact_from_date,
    @empl,
    @c_d_code,
    @sact_to_date,
    @sact_code,
    @dmnd_from_date,
    @dmnd_to_date,
    @dmnd_code,
    @prv_days,
    @sact_officer,
    @reason,
    @reason_Remark,
    @cherge_to,
    @effect_pay_date,
    @Leave_App_ID,
    @Operator,
    @Status
);";


                    // =============================================
                    // SANCTION FROM
                    // =============================================
                    AddParameter(
                        command,
                        "@sact_from_date",
                        model.SanctionFromDate.Value,
                        DbType.DateTime);


                    // =============================================
                    // EMPLOYEE
                    // =============================================
                    AddParameter(
                        command,
                        "@empl",
                        model.EmployeeCode,
                        DbType.Int32);


                    // =============================================
                    // C/D CODE
                    // ALWAYS 2
                    // =============================================
                    AddParameter(
                        command,
                        "@c_d_code",
                        cdCode,
                        DbType.Int16);


                    // =============================================
                    // SANCTION TO
                    // =============================================
                    AddParameter(
                        command,
                        "@sact_to_date",
                        model.SanctionToDate,
                        DbType.DateTime);


                    // =============================================
                    // SANCTION LEAVE
                    // =============================================
                    AddParameter(
                        command,
                        "@sact_code",
                        model.SanctionLeaveCode.Value,
                        DbType.Int32);


                    // =============================================
                    // DEMAND FROM
                    // =============================================
                    AddParameter(
                        command,
                        "@dmnd_from_date",
                        model.DemandFromDate.Value,
                        DbType.DateTime);


                    // =============================================
                    // DEMAND TO
                    // =============================================
                    AddParameter(
                        command,
                        "@dmnd_to_date",
                        model.DemandToDate.Value,
                        DbType.DateTime);


                    // =============================================
                    // DEMAND LEAVE
                    // =============================================
                    AddParameter(
                        command,
                        "@dmnd_code",
                        model.DemandLeaveCode.Value,
                        DbType.Int32);


                    // =============================================
                    // PREVIOUS DAYS
                    // =============================================
                    AddParameter(
                        command,
                        "@prv_days",
                        model.PreviousDays,
                        DbType.Double);


                    // =============================================
                    // SANCTION OFFICER
                    // EMPLOYEE CODE AUTOMATIC
                    // =============================================
                    AddParameter(
                        command,
                        "@sact_officer",
                        sanctionOfficerCode,
                        DbType.Int32);


                    // =============================================
                    // REASON CODE
                    // NO CHANGE
                    // USER ENTERED VALUE IS SAVED
                    // =============================================
                    AddParameter(
                        command,
                        "@reason",
                        model.ReasonCode,
                        DbType.Int32);


                    // =============================================
                    // REASON REMARK
                    // =============================================
                    AddParameter(
                        command,
                        "@reason_Remark",
                        string.IsNullOrWhiteSpace(
                            model.ReasonRemark)
                            ? null
                            : model.ReasonRemark.Trim(),
                        DbType.String,
                        80);


                    // =============================================
                    // CHARGE TO TRANSFER
                    //
                    // ONLY EMPLOYEE CODE STORED IN DATABASE
                    // NAME COMES FROM PayMast WHILE DISPLAYING
                    // =============================================
                    AddParameter(
                        command,
                        "@cherge_to",
                        model.ChargeTo,
                        DbType.Int32);


                    // =============================================
                    // EFFECT PAY DATE
                    // =============================================
                    AddParameter(
                        command,
                        "@effect_pay_date",
                        model.EffectPayDate,
                        DbType.DateTime);


                    // =============================================
                    // LEAVE APPLICATION ID
                    // =============================================
                    AddParameter(
                        command,
                        "@Leave_App_ID",
                        newLeaveApplicationId,
                        DbType.Decimal);


                    // =============================================
                    // OPERATOR
                    // EMPLOYEE CODE AUTOMATIC
                    // =============================================
                    AddParameter(
                        command,
                        "@Operator",
                        operatorCode,
                        DbType.String,
                        50);


                    // =============================================
                    // STATUS
                    // =============================================
                    AddParameter(
                        command,
                        "@Status",
                        "P",
                        DbType.String,
                        20);


                    // =============================================
                    // EXECUTE
                    // =============================================
                    int rowsAffected =
                        await command.ExecuteNonQueryAsync();


                    if (rowsAffected <= 0)
                    {
                        TempData["Error"] =
                            "Leave application could not be saved.";

                        return RedirectToAction(
                            nameof(Index));
                    }
                }
                finally
                {
                    if (shouldCloseConnection &&
                        connection.State ==
                        ConnectionState.Open)
                    {
                        await connection.CloseAsync();
                    }
                }


                TempData["Success"] =
                    $"Leave Application ID {newLeaveApplicationId} saved successfully.";


                return RedirectToAction(
                    nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    ex.Message;

                await LoadLeaveTypes(model);

                return View(
                    "Index",
                    model);
            }
        }


        // =========================================================
        // SANCTION LIST
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> SanctionList()
        {
            List<LeaveSanctionListViewModel> list =
                new List<LeaveSanctionListViewModel>();


            DbConnection connection =
                _context.Database.GetDbConnection();

            bool shouldCloseConnection =
                connection.State != ConnectionState.Open;


            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }


            try
            {
                using DbCommand command =
                    connection.CreateCommand();


                command.CommandText = @"
SELECT
    l.Leave_App_ID,
    l.empl,
    p.name AS EmployeeName,

    l.c_d_code,

    l.dmnd_from_date,
    l.dmnd_to_date,
    l.dmnd_code,
    dl.name AS DemandLeaveName,

    l.prv_days,
    l.reason,
    l.reason_Remark,

    l.cherge_to,

    chargeEmp.name AS ChargeToEmployeeName,

    l.sact_from_date,
    l.sact_to_date,
    l.sact_code,
    sl.name AS SanctionLeaveName,

    l.sact_officer,
    l.effect_pay_date,
    l.Operator,

    ISNULL(NULLIF(LTRIM(RTRIM(l.Status)), ''), 'P') AS Status

FROM dbo.leavtrn l

LEFT JOIN dbo.PayMast p
    ON p.code = l.empl

LEFT JOIN dbo.PayMast chargeEmp
    ON chargeEmp.code = l.cherge_to

LEFT JOIN dbo.leavmast dl
    ON dl.code = l.dmnd_code

LEFT JOIN dbo.leavmast sl
    ON sl.code = l.sact_code

ORDER BY l.Leave_App_ID DESC;";


                using DbDataReader reader =
                    await command.ExecuteReaderAsync();


                while (await reader.ReadAsync())
                {
                    list.Add(
                        MapLeaveReader(reader));
                }
            }
            finally
            {
                if (shouldCloseConnection &&
                    connection.State ==
                    ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }


            return View(list);
        }


        // =========================================================
        // VIEW LEAVE
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> ViewLeave(
            decimal id)
        {
            LeaveSanctionListViewModel? model = null;


            DbConnection connection =
                _context.Database.GetDbConnection();

            bool shouldCloseConnection =
                connection.State != ConnectionState.Open;


            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }


            try
            {
                using DbCommand command =
                    connection.CreateCommand();


                command.CommandText = @"
SELECT
    l.Leave_App_ID,
    l.empl,
    p.name AS EmployeeName,

    l.c_d_code,

    l.dmnd_from_date,
    l.dmnd_to_date,
    l.dmnd_code,
    dl.name AS DemandLeaveName,

    l.prv_days,
    l.reason,
    l.reason_Remark,

    l.cherge_to,

    chargeEmp.name AS ChargeToEmployeeName,

    l.sact_from_date,
    l.sact_to_date,
    l.sact_code,
    sl.name AS SanctionLeaveName,

    l.sact_officer,
    l.effect_pay_date,
    l.Operator,

    ISNULL(NULLIF(LTRIM(RTRIM(l.Status)), ''), 'P') AS Status

FROM dbo.leavtrn l

LEFT JOIN dbo.PayMast p
    ON p.code = l.empl

LEFT JOIN dbo.PayMast chargeEmp
    ON chargeEmp.code = l.cherge_to

LEFT JOIN dbo.leavmast dl
    ON dl.code = l.dmnd_code

LEFT JOIN dbo.leavmast sl
    ON sl.code = l.sact_code

WHERE l.Leave_App_ID = @id;";


                AddParameter(
                    command,
                    "@id",
                    id,
                    DbType.Decimal);


                using DbDataReader reader =
                    await command.ExecuteReaderAsync();


                if (await reader.ReadAsync())
                {
                    model =
                        MapLeaveReader(reader);
                }
            }
            finally
            {
                if (shouldCloseConnection &&
                    connection.State ==
                    ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }


            if (model == null)
            {
                TempData["Error"] =
                    "Leave application not found.";

                return RedirectToAction(
                    nameof(SanctionList));
            }


            // Admin can choose the final sanction leave type while the request is Pending.
            ViewBag.SanctionLeaveTypes = await LoadSanctionLeaveTypes();

            return View(model);
        }


        // =========================================================
        // APPROVE LEAVE
        //
        // ADMIN CAN EDIT:
        // Sanction From
        // Sanction To
        //
        // BOTH VALUES ARE UPDATED IN DATABASE
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(
            LeaveSanctionListViewModel model)
        {
            if (!model.LeaveApplicationId.HasValue)
            {
                TempData["Error"] =
                    "Invalid Leave Application ID.";

                return RedirectToAction(
                    nameof(SanctionList));
            }


            decimal leaveApplicationId =
                model.LeaveApplicationId.Value;


            if (!model.SanctionFromDate.HasValue)
            {
                TempData["Error"] =
                    "Sanction From Date is required.";

                return RedirectToAction(
                    nameof(ViewLeave),
                    new
                    {
                        id = leaveApplicationId
                    });
            }


            if (!model.SanctionToDate.HasValue)
            {
                TempData["Error"] =
                    "Sanction To Date is required.";

                return RedirectToAction(
                    nameof(ViewLeave),
                    new
                    {
                        id = leaveApplicationId
                    });
            }


            if (model.SanctionToDate.Value.Date <
                model.SanctionFromDate.Value.Date)
            {
                TempData["Error"] =
                    "Sanction To Date cannot be less than Sanction From Date.";

                return RedirectToAction(
                    nameof(ViewLeave),
                    new
                    {
                        id = leaveApplicationId
                    });
            }


            if (!model.NumberOfDays.HasValue ||
                model.NumberOfDays.Value <= 0)
            {
                TempData["Error"] =
                    "Please enter valid Days.";

                return RedirectToAction(
                    nameof(ViewLeave),
                    new
                    {
                        id = leaveApplicationId
                    });
            }

            if (!model.SanctionLeaveCode.HasValue ||
                model.SanctionLeaveCode.Value <= 0 ||
                !await LeaveCodeExists(model.SanctionLeaveCode.Value))
            {
                TempData["Error"] = "Please select a valid Sanction Leave Type.";
                return RedirectToAction(nameof(ViewLeave), new { id = leaveApplicationId });
            }


            DbConnection connection =
                _context.Database.GetDbConnection();

            bool shouldCloseConnection =
                connection.State != ConnectionState.Open;


            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }


            try
            {
                using DbCommand command =
                    connection.CreateCommand();


                command.CommandText = @"
UPDATE dbo.leavtrn

SET
    sact_from_date = @sact_from_date,
    sact_to_date   = @sact_to_date,
    sact_code      = @sact_code,
    Num_of_days    = @Num_of_days,
    Status         = @Status,
    Operator       = @Operator

WHERE Leave_App_ID = @Leave_App_ID
  AND UPPER(LTRIM(RTRIM(ISNULL(Status, 'P')))) IN ('P', 'PENDING');";


                AddParameter(
                    command,
                    "@sact_from_date",
                    model.SanctionFromDate.Value,
                    DbType.DateTime);


                AddParameter(
                    command,
                    "@sact_to_date",
                    model.SanctionToDate.Value,
                    DbType.DateTime);


                // Final leave type selected by Admin (10 = WITHOUT PAY LEAVE).
                AddParameter(
                    command,
                    "@sact_code",
                    model.SanctionLeaveCode.Value,
                    DbType.Int32);


                // =============================================
                // NUMBER OF DAYS
                // Required because UPDATE uses @Num_of_days
                // =============================================
                AddParameter(
                    command,
                    "@Num_of_days",
                    model.NumberOfDays.Value,
                    DbType.Double);


                AddParameter(
                    command,
                    "@Status",
                    "A",
                    DbType.String,
                    20);


                // Logged-in user who approved this leave.
                // String value preserves leading zero, e.g. 0119.
                string operatorCode =
                    HttpContext.Session.GetString("LoginId") ?? "";

                AddParameter(
                    command,
                    "@Operator",
                    operatorCode,
                    DbType.String,
                    50);


                AddParameter(
                    command,
                    "@Leave_App_ID",
                    leaveApplicationId,
                    DbType.Decimal);


                int rowsAffected =
                    await command.ExecuteNonQueryAsync();


                if (rowsAffected <= 0)
                {
                    TempData["Error"] =
                        "Leave application not found or already processed.";

                    return RedirectToAction(
                        nameof(SanctionList));
                }


                try
                {
                    int updated = await SyncPayrollForLeave(leaveApplicationId);
                    TempData["Success"] = $"Leave Application ID {leaveApplicationId} approved. {updated} saved payroll month(s) synchronized.";
                }
                catch (Exception syncError)
                {
                    TempData["Error"] = $"Leave approved, but payroll synchronization failed: {syncError.Message}. Please resolve before using Salary Report.";
                }


                return RedirectToAction(
                    nameof(SanctionList));
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    ex.Message;


                return RedirectToAction(
                    nameof(ViewLeave),
                    new
                    {
                        id = leaveApplicationId
                    });
            }
            finally
            {
                if (shouldCloseConnection &&
                    connection.State ==
                    ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }


        // =========================================================
        // REJECT LEAVE
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(
            LeaveSanctionListViewModel model)
        {
            if (!model.LeaveApplicationId.HasValue)
            {
                TempData["Error"] =
                    "Invalid Leave Application ID.";

                return RedirectToAction(
                    nameof(SanctionList));
            }


            decimal id =
                model.LeaveApplicationId.Value;


            int rowsAffected =
                await UpdateLeaveStatus(
                    id,
                    "R");


            if (rowsAffected <= 0)
            {
                TempData["Error"] =
                    "Leave application not found or already processed.";
            }
            else
            {
                try
                {
                    int updated = await SyncPayrollForLeave(id);
                    TempData["Success"] = $"Leave Application ID {id} rejected. {updated} saved payroll month(s) synchronized.";
                }
                catch (Exception syncError)
                {
                    TempData["Error"] = $"Leave rejected, but payroll synchronization failed: {syncError.Message}. Please resolve before using Salary Report.";
                }
            }


            return RedirectToAction(
                nameof(SanctionList));
        }


        // Synchronize saved months touched by this leave. Unsaved months are
        // calculated on demand by the Pay Calculation dashboard.
        private async Task<int> SyncPayrollForLeave(decimal leaveApplicationId)
        {
            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;
            int employeeCode;
            DateTime fromDate, toDate;
            try
            {
                if (close) await connection.OpenAsync();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"SELECT TOP 1 empl, sact_from_date, sact_to_date
 FROM dbo.leavtrn WHERE Leave_App_ID=@Id;";
                AddParameter(cmd, "@Id", leaveApplicationId, DbType.Decimal);
                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return 0;
                employeeCode = Convert.ToInt32(reader["empl"]);
                if (reader["sact_from_date"] == DBNull.Value || reader["sact_to_date"] == DBNull.Value)
                    return 0;
                fromDate = Convert.ToDateTime(reader["sact_from_date"]);
                toDate = Convert.ToDateTime(reader["sact_to_date"]);
            }
            finally
            {
                if (close && connection.State == ConnectionState.Open) await connection.CloseAsync();
            }
            if (toDate < fromDate) return 0;
            var payroll = new PayCalculationController(_context);
            int updated = 0;
            var month = new DateTime(fromDate.Year, fromDate.Month, 1);
            var last = new DateTime(toDate.Year, toDate.Month, 1);
            if ((last.Year - month.Year) * 12 + last.Month - month.Month > 120)
                throw new InvalidOperationException("Leave period exceeds 120 months.");
            while (month <= last)
            {
                if (await payroll.RecalculateSavedEmployeeAfterLeave(employeeCode, month.Year, month.Month))
                    updated++;
                month = month.AddMonths(1);
            }
            return updated;
        }

        // =========================================================
        // UPDATE LEAVE STATUS
        // =========================================================
        private async Task<int> UpdateLeaveStatus(
            decimal id,
            string status)
        {
            DbConnection connection =
                _context.Database.GetDbConnection();

            bool shouldCloseConnection =
                connection.State != ConnectionState.Open;


            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }


            try
            {
                using DbCommand command =
                    connection.CreateCommand();


                command.CommandText = @"
UPDATE dbo.leavtrn

SET
    Status   = @Status,
    Operator = @Operator

WHERE Leave_App_ID = @id
  AND UPPER(LTRIM(RTRIM(ISNULL(Status, 'P')))) IN ('P', 'PENDING', 'A', 'APPROVED');";


                AddParameter(
                    command,
                    "@Status",
                    status,
                    DbType.String,
                    20);


                string operatorCode =
                    HttpContext.Session.GetString("LoginId") ?? "";

                AddParameter(
                    command,
                    "@Operator",
                    operatorCode,
                    DbType.String,
                    50);


                AddParameter(
                    command,
                    "@id",
                    id,
                    DbType.Decimal);


                return await command
                    .ExecuteNonQueryAsync();
            }
            finally
            {
                if (shouldCloseConnection &&
                    connection.State ==
                    ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }


        // =========================================================
        // MAP LEAVE READER
        // =========================================================
        private static LeaveSanctionListViewModel
            MapLeaveReader(DbDataReader reader)
        {
            return new LeaveSanctionListViewModel
            {
                // =================================================
                // LEAVE APPLICATION ID
                // =================================================
                LeaveApplicationId =
                    reader["Leave_App_ID"] == DBNull.Value
                        ? null
                        : Convert.ToDecimal(
                            reader["Leave_App_ID"]),


                // =================================================
                // EMPLOYEE
                // =================================================
                EmployeeCode =
                    reader["empl"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(
                            reader["empl"]),


                EmployeeName =
                    reader["EmployeeName"] == DBNull.Value
                        ? ""
                        : Convert.ToString(
                            reader["EmployeeName"]),


                // =================================================
                // C/D CODE
                //
                // STILL READ FROM DATABASE.
                // DO NOT DISPLAY ON ViewLeave DASHBOARD.
                // =================================================
                CDCode =
                    reader["c_d_code"] == DBNull.Value
                        ? null
                        : Convert.ToInt16(
                            reader["c_d_code"]),


                // =================================================
                // DEMAND
                // =================================================
                DemandFromDate =
                    reader["dmnd_from_date"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(
                            reader["dmnd_from_date"]),


                DemandToDate =
                    reader["dmnd_to_date"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(
                            reader["dmnd_to_date"]),


                DemandLeaveCode =
                    reader["dmnd_code"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(
                            reader["dmnd_code"]),


                DemandLeaveName =
                    reader["DemandLeaveName"] == DBNull.Value
                        ? ""
                        : Convert.ToString(
                            reader["DemandLeaveName"]),


                PreviousDays =
                    reader["prv_days"] == DBNull.Value
                        ? null
                        : Convert.ToDouble(
                            reader["prv_days"]),


                // =================================================
                // REASON
                // NO CHANGE
                // =================================================
                ReasonCode =
                    reader["reason"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(
                            reader["reason"]),


                ReasonRemark =
                    reader["reason_Remark"] == DBNull.Value
                        ? ""
                        : Convert.ToString(
                            reader["reason_Remark"]),


                // =================================================
                // CHARGE TO TRANSFER
                // =================================================
                ChargeTo =
                    reader["cherge_to"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(
                            reader["cherge_to"]),


                ChargeToEmployeeName =
                    reader["ChargeToEmployeeName"] == DBNull.Value
                        ? ""
                        : Convert.ToString(
                            reader["ChargeToEmployeeName"]),


                // =================================================
                // SANCTION
                // =================================================
                SanctionFromDate =
                    reader["sact_from_date"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(
                            reader["sact_from_date"]),


                SanctionToDate =
                    reader["sact_to_date"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(
                            reader["sact_to_date"]),


                SanctionLeaveCode =
                    reader["sact_code"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(
                            reader["sact_code"]),


                SanctionLeaveName =
                    reader["SanctionLeaveName"] == DBNull.Value
                        ? ""
                        : Convert.ToString(
                            reader["SanctionLeaveName"]),


                SanctionOfficer =
                    reader["sact_officer"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(
                            reader["sact_officer"]),


                EffectPayDate =
                    reader["effect_pay_date"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(
                            reader["effect_pay_date"]),


                // =================================================
                // OPERATOR
                // =================================================
                Operator =
                    reader["Operator"] == DBNull.Value
                        ? ""
                        : Convert.ToString(
                            reader["Operator"]),


                // =================================================
                // STATUS
                // =================================================
                Status =
                    reader["Status"] == DBNull.Value
                        ? "P"
                        : Convert.ToString(
                            reader["Status"])
            };
        }


        // =========================================================
        // LOAD SANCTION LEAVE TYPES FOR ADMIN
        // Includes active dbo.leavmast rows, including code 10 WPL.
        // =========================================================
        private async Task<List<SelectListItem>> LoadSanctionLeaveTypes()
        {
            var list = new List<SelectListItem>();
            DbConnection connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;
            try
            {
                if (close) await connection.OpenAsync();
                using DbCommand command = connection.CreateCommand();
                command.CommandText = @"
SELECT code, ISNULL(name,'') name
FROM dbo.leavmast
WHERE ISNULL(Active,'N')='Y'
ORDER BY TRY_CONVERT(INT,code);";
                using DbDataReader reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int code = Convert.ToInt32(reader["code"]);
                    string name = Convert.ToString(reader["name"])?.Trim() ?? "";
                    list.Add(new SelectListItem { Value = code.ToString(), Text = $"{code} - {name}" });
                }
            }
            finally
            {
                if (close && connection.State == ConnectionState.Open) await connection.CloseAsync();
            }
            return list;
        }


        // =========================================================
        // LOAD LEAVE TYPES
        // =========================================================
        private async Task LoadLeaveTypes(
            LeaveApplicationViewModel model)
        {
            model.LeaveTypes =
                new List<SelectListItem>();


            DbConnection connection =
                _context.Database.GetDbConnection();

            bool shouldCloseConnection =
                connection.State != ConnectionState.Open;


            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }


            try
            {
                using DbCommand command =
                    connection.CreateCommand();


                command.CommandText = @"
SELECT code, name
FROM dbo.leavmast
WHERE ISNULL(Active, 'N') = 'Y'
ORDER BY code;";


                using DbDataReader reader =
                    await command.ExecuteReaderAsync();


                List<LeaveTypeData> leaveList =
                    new List<LeaveTypeData>();


                while (await reader.ReadAsync())
                {
                    leaveList.Add(
                        new LeaveTypeData
                        {
                            Code =
                                Convert.ToInt32(
                                    reader["code"]),

                            Name =
                                reader["name"] == DBNull.Value
                                    ? ""
                                    : Convert.ToString(
                                        reader["name"]) ?? ""
                        });
                }


                model.LeaveTypes =
                    leaveList
                        .OrderBy(x => x.Code)
                        .Select(x =>
                            new SelectListItem
                            {
                                Value =
                                    x.Code.ToString(),

                                Text =
                                    $"{x.Code} - {x.Name}"
                            })
                        .ToList();
            }
            finally
            {
                if (shouldCloseConnection &&
                    connection.State ==
                    ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }


        // =========================================================
        // LEAVE CODE EXISTS
        // =========================================================
        private async Task<bool> LeaveCodeExists(
            int leaveCode)
        {
            DbConnection connection =
                _context.Database.GetDbConnection();

            bool shouldCloseConnection =
                connection.State != ConnectionState.Open;


            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }


            try
            {
                using DbCommand command =
                    connection.CreateCommand();


                command.CommandText = @"
SELECT COUNT(1)
FROM dbo.leavmast
WHERE code = @code
  AND ISNULL(Active, 'N') = 'Y';";


                AddParameter(
                    command,
                    "@code",
                    leaveCode,
                    DbType.Int32);


                object? result =
                    await command.ExecuteScalarAsync();


                return Convert.ToInt32(
                    result ?? 0) > 0;
            }
            finally
            {
                if (shouldCloseConnection &&
                    connection.State ==
                    ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }


        // =========================================================
        // MATERNITY LEAVE HELPERS
        // =========================================================
        private async Task<bool> IsMaternityLeave(int leaveCode)
        {
            string? leaveName = await _context.LeaveMasters
                .AsNoTracking()
                .Where(x => x.Code == leaveCode && x.Active == "Y")
                .Select(x => x.Name)
                .FirstOrDefaultAsync();

            return !string.IsNullOrWhiteSpace(leaveName) &&
                   leaveName.Contains("MATERNITY",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFemale(string? gender)
        {
            string value = (gender ?? "").Trim();

            return value.Equals("F", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("FEMALE", StringComparison.OrdinalIgnoreCase);
        }


        // =========================================================
        // PARAMETER HELPER
        // =========================================================
        private static void AddParameter(
            DbCommand command,
            string parameterName,
            object? value,
            DbType dbType,
            int? size = null)
        {
            DbParameter parameter =
                command.CreateParameter();


            parameter.ParameterName =
                parameterName;


            parameter.DbType =
                dbType;


            if (size.HasValue)
            {
                parameter.Size =
                    size.Value;
            }


            parameter.Value =
                value ?? DBNull.Value;


            command.Parameters.Add(
                parameter);
        }


        // =========================================================
        // LEAVE TYPE TEMP CLASS
        // =========================================================
        private class LeaveTypeData
        {
            public int Code { get; set; }

            public string Name { get; set; }
                = string.Empty;
        }
    }
}