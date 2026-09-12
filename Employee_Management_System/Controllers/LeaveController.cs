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
            LeaveApplicationViewModel model =
                new LeaveApplicationViewModel();

            await LoadLeaveTypes(model);

            return View(model);
        }


        // =========================================================
        // GET EMPLOYEE
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> GetEmployee(
            int employeeCode)
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


                var employee =
                    await _context.PayMasts
                        .AsNoTracking()
                        .Where(x =>
                            x.EmployeeCode == employeeCode)
                        .Select(x => new
                        {
                            employeeCode = x.EmployeeCode,
                            employeeName = x.EmployeeName
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


                bool employeeExists =
                    await _context.PayMasts
                        .AsNoTracking()
                        .AnyAsync(x =>
                            x.EmployeeCode ==
                            model.EmployeeCode);


                if (!employeeExists)
                {
                    TempData["Error"] =
                        "Employee not found.";

                    await LoadLeaveTypes(model);

                    return View("Index", model);
                }


                // =================================================
                // FROM DATE
                // =================================================

                if (!model.DemandFromDate.HasValue)
                {
                    TempData["Error"] =
                        "From Date is required.";

                    await LoadLeaveTypes(model);

                    return View("Index", model);
                }


                // =================================================
                // TO DATE
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
                // LEAVE TYPE
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
                // CONNECTION
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
                    // =================================================
                    // AUTO LEAVE APP ID
                    // =================================================

                    using (DbCommand idCommand =
                           connection.CreateCommand())
                    {
                        idCommand.CommandText = @"
SELECT ISNULL(MAX(Leave_App_ID), 0) + 1
FROM dbo.leavtrn;";


                        object? result =
                            await idCommand.ExecuteScalarAsync();


                        newLeaveApplicationId =
                            Convert.ToDecimal(
                                result ?? 1);
                    }


                    // =================================================
                    // INSERT
                    // =================================================

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


                    AddParameter(
                        command,
                        "@sact_from_date",
                        model.SanctionFromDate.Value,
                        DbType.DateTime);


                    AddParameter(
                        command,
                        "@empl",
                        model.EmployeeCode,
                        DbType.Int32);


                    // C/D Code automatic = 2

                    AddParameter(
                        command,
                        "@c_d_code",
                        (short)2,
                        DbType.Int16);


                    AddParameter(
                        command,
                        "@sact_to_date",
                        model.SanctionToDate,
                        DbType.DateTime);


                    AddParameter(
                        command,
                        "@sact_code",
                        model.SanctionLeaveCode.Value,
                        DbType.Int32);


                    AddParameter(
                        command,
                        "@dmnd_from_date",
                        model.DemandFromDate.Value,
                        DbType.DateTime);


                    AddParameter(
                        command,
                        "@dmnd_to_date",
                        model.DemandToDate.Value,
                        DbType.DateTime);


                    AddParameter(
                        command,
                        "@dmnd_code",
                        model.DemandLeaveCode.Value,
                        DbType.Int32);


                    AddParameter(
                        command,
                        "@prv_days",
                        model.PreviousDays,
                        DbType.Double);


                    AddParameter(
                        command,
                        "@sact_officer",
                        model.SanctionOfficer,
                        DbType.Int32);


                    AddParameter(
                        command,
                        "@reason",
                        model.ReasonCode,
                        DbType.Int32);


                    AddParameter(
                        command,
                        "@reason_Remark",
                        string.IsNullOrWhiteSpace(
                            model.ReasonRemark)
                            ? null
                            : model.ReasonRemark.Trim(),
                        DbType.String,
                        80);


                    AddParameter(
                        command,
                        "@cherge_to",
                        model.ChargeTo,
                        DbType.Int32);


                    AddParameter(
                        command,
                        "@effect_pay_date",
                        model.EffectPayDate,
                        DbType.DateTime);


                    AddParameter(
                        command,
                        "@Leave_App_ID",
                        newLeaveApplicationId,
                        DbType.Decimal);


                    AddParameter(
                        command,
                        "@Operator",
                        null,
                        DbType.String,
                        50);


                    // NEW LEAVE = PENDING

                    AddParameter(
                        command,
                        "@Status",
                        "Pending",
                        DbType.String,
                        20);


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

    l.sact_from_date,
    l.sact_to_date,
    l.sact_code,
    sl.name AS SanctionLeaveName,

    l.sact_officer,
    l.effect_pay_date,
    l.Operator,

    ISNULL(l.Status, 'Pending') AS Status

FROM dbo.leavtrn l

LEFT JOIN dbo.PayMast p
    ON p.code = l.empl

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
        // VIEW ONE LEAVE
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> ViewLeave(
            decimal id)
        {
            LeaveSanctionListViewModel? model =
                null;


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

    l.sact_from_date,
    l.sact_to_date,
    l.sact_code,
    sl.name AS SanctionLeaveName,

    l.sact_officer,
    l.effect_pay_date,
    l.Operator,

    ISNULL(l.Status, 'Pending') AS Status

FROM dbo.leavtrn l

LEFT JOIN dbo.PayMast p
    ON p.code = l.empl

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


            return View(model);
        }


        // =========================================================
        // APPROVE
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(
            decimal id)
        {
            int rowsAffected =
                await UpdateLeaveStatus(
                    id,
                    "Approved");


            if (rowsAffected <= 0)
            {
                TempData["Error"] =
                    "Leave application not found.";
            }
            else
            {
                TempData["Success"] =
                    $"Leave Application ID {id} approved successfully.";
            }


            return RedirectToAction(
                nameof(SanctionList));
        }


        // =========================================================
        // REJECT
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(
            decimal id)
        {
            int rowsAffected =
                await UpdateLeaveStatus(
                    id,
                    "Rejected");


            if (rowsAffected <= 0)
            {
                TempData["Error"] =
                    "Leave application not found.";
            }
            else
            {
                TempData["Success"] =
                    $"Leave Application ID {id} rejected successfully.";
            }


            return RedirectToAction(
                nameof(SanctionList));
        }


        // =========================================================
        // UPDATE STATUS
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

SET Status = @Status

WHERE Leave_App_ID = @id;";


                AddParameter(
                    command,
                    "@Status",
                    status,
                    DbType.String,
                    20);


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
        // MAP LEAVE RECORD
        // =========================================================

        private static LeaveSanctionListViewModel
            MapLeaveReader(
                DbDataReader reader)
        {
            return new LeaveSanctionListViewModel
            {
                LeaveApplicationId =
                    reader["Leave_App_ID"] ==
                    DBNull.Value
                        ? null
                        : Convert.ToDecimal(
                            reader["Leave_App_ID"]),


                EmployeeCode =
                    reader["empl"] ==
                    DBNull.Value
                        ? 0
                        : Convert.ToInt32(
                            reader["empl"]),


                EmployeeName =
                    reader["EmployeeName"] ==
                    DBNull.Value
                        ? ""
                        : Convert.ToString(
                            reader["EmployeeName"]),


                CDCode =
                    reader["c_d_code"] ==
                    DBNull.Value
                        ? null
                        : Convert.ToInt16(
                            reader["c_d_code"]),


                DemandFromDate =
                    reader["dmnd_from_date"] ==
                    DBNull.Value
                        ? null
                        : Convert.ToDateTime(
                            reader["dmnd_from_date"]),


                DemandToDate =
                    reader["dmnd_to_date"] ==
                    DBNull.Value
                        ? null
                        : Convert.ToDateTime(
                            reader["dmnd_to_date"]),


                DemandLeaveCode =
                    reader["dmnd_code"] ==
                    DBNull.Value
                        ? null
                        : Convert.ToInt32(
                            reader["dmnd_code"]),


                DemandLeaveName =
                    reader["DemandLeaveName"] ==
                    DBNull.Value
                        ? ""
                        : Convert.ToString(
                            reader["DemandLeaveName"]),


                PreviousDays =
                    reader["prv_days"] ==
                    DBNull.Value
                        ? null
                        : Convert.ToDouble(
                            reader["prv_days"]),


                ReasonCode =
                    reader["reason"] ==
                    DBNull.Value
                        ? null
                        : Convert.ToInt32(
                            reader["reason"]),


                ReasonRemark =
                    reader["reason_Remark"] ==
                    DBNull.Value
                        ? ""
                        : Convert.ToString(
                            reader["reason_Remark"]),


                ChargeTo =
                    reader["cherge_to"] ==
                    DBNull.Value
                        ? null
                        : Convert.ToInt32(
                            reader["cherge_to"]),


                SanctionFromDate =
                    reader["sact_from_date"] ==
                    DBNull.Value
                        ? null
                        : Convert.ToDateTime(
                            reader["sact_from_date"]),


                SanctionToDate =
                    reader["sact_to_date"] ==
                    DBNull.Value
                        ? null
                        : Convert.ToDateTime(
                            reader["sact_to_date"]),


                SanctionLeaveCode =
                    reader["sact_code"] ==
                    DBNull.Value
                        ? null
                        : Convert.ToInt32(
                            reader["sact_code"]),


                SanctionLeaveName =
                    reader["SanctionLeaveName"] ==
                    DBNull.Value
                        ? ""
                        : Convert.ToString(
                            reader["SanctionLeaveName"]),


                SanctionOfficer =
                    reader["sact_officer"] ==
                    DBNull.Value
                        ? null
                        : Convert.ToInt32(
                            reader["sact_officer"]),


                EffectPayDate =
                    reader["effect_pay_date"] ==
                    DBNull.Value
                        ? null
                        : Convert.ToDateTime(
                            reader["effect_pay_date"]),


                Operator =
                    reader["Operator"] ==
                    DBNull.Value
                        ? ""
                        : Convert.ToString(
                            reader["Operator"]),


                Status =
                    reader["Status"] ==
                    DBNull.Value
                        ? "Pending"
                        : Convert.ToString(
                            reader["Status"])
            };
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
FROM dbo.leavmast;";


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
                                reader["name"] ==
                                DBNull.Value
                                    ? ""
                                    : Convert.ToString(
                                        reader["name"])
                                      ?? ""
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
WHERE code = @code;";


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