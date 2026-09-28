using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace Employee_Management_System.Controllers
{
    public class LeaveStatementController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LeaveStatementController(
            ApplicationDbContext context)
        {
            _context = context;
        }


        // =========================================================
        // LEAVE STATEMENT PAGE
        // =========================================================
        [HttpGet]
        public IActionResult Index()
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            int year = DateTime.Now.Year;

            var model =
                new LeaveStatementViewModel
                {
                    Year = year,

                    SanctionFrom =
                        new DateTime(
                            year,
                            1,
                            1),

                    SanctionTo =
                        new DateTime(
                            year,
                            12,
                            31)
                };

            return View(model);
        }


        // =========================================================
        // SEARCH LEAVE STATEMENT
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Search(
            int? year,
            int? employeeCode,
            DateTime? sanctionFrom,
            DateTime? sanctionTo)
        {
            // =====================================================
            // SESSION CHECK
            // =====================================================

            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }


            // =====================================================
            // MODEL
            // =====================================================

            var model =
                new LeaveStatementViewModel
                {
                    Year = year,

                    EmployeeCode =
                        employeeCode,

                    SanctionFrom =
                        sanctionFrom,

                    SanctionTo =
                        sanctionTo,

                    IsSearched =
                        true
                };


            // =====================================================
            // VALIDATION - YEAR
            // =====================================================

            if (!year.HasValue)
            {
                model.ErrorMessage =
                    "Please select year.";

                return View(
                    "Index",
                    model);
            }


            // =====================================================
            // VALIDATION - EMPLOYEE
            // =====================================================

            if (!employeeCode.HasValue ||
                employeeCode.Value <= 0)
            {
                model.ErrorMessage =
                    "Please enter employee code.";

                return View(
                    "Index",
                    model);
            }


            // =====================================================
            // VALIDATION - SANCTION FROM
            // =====================================================

            if (!sanctionFrom.HasValue)
            {
                model.ErrorMessage =
                    "Please select sanction from date.";

                return View(
                    "Index",
                    model);
            }


            // =====================================================
            // VALIDATION - SANCTION TO
            // =====================================================

            if (!sanctionTo.HasValue)
            {
                model.ErrorMessage =
                    "Please select sanction to date.";

                return View(
                    "Index",
                    model);
            }


            // =====================================================
            // VALIDATION - DATE RANGE
            // =====================================================

            if (sanctionFrom.Value.Date >
                sanctionTo.Value.Date)
            {
                model.ErrorMessage =
                    "Sanction From date cannot be greater than Sanction To date.";

                return View(
                    "Index",
                    model);
            }


            // =====================================================
            // GET EMPLOYEE DETAILS FROM PAYMAST
            // =====================================================

            var employee =
                await _context.PayMasts
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.EmployeeCode ==
                            employeeCode.Value)
                    .Select(
                        x => new
                        {
                            x.EmployeeCode,
                            x.EmployeeName,
                            x.JoiningDate
                        })
                    .FirstOrDefaultAsync();


            // =====================================================
            // EMPLOYEE NOT FOUND
            // =====================================================

            if (employee == null)
            {
                model.ErrorMessage =
                    "Employee not found.";

                return View(
                    "Index",
                    model);
            }


            // =====================================================
            // EMPLOYEE NAME
            // =====================================================

            model.EmployeeName =
                employee.EmployeeName;

            // Joining Date from dbo.PayMast.join_date
            model.JoiningDate =
                employee.JoiningDate;


            // =====================================================
            // DATABASE CONNECTION
            // =====================================================

            DbConnection connection =
                _context.Database
                    .GetDbConnection();

            bool closeConnection =
                false;


            try
            {
                // =================================================
                // OPEN CONNECTION
                // =================================================

                if (connection.State !=
                    ConnectionState.Open)
                {
                    await connection
                        .OpenAsync();

                    closeConnection =
                        true;
                }


                // =================================================
                // OPENING BALANCE BEFORE SELECTED FROM DATE
                // =================================================
                double openingBalance = 0;

                using (DbCommand openingCommand = connection.CreateCommand())
                {
                    openingCommand.CommandText = @"
SELECT
    ISNULL(SUM(CASE WHEN c_d_code = 1 AND ISNULL(Status,'') = 'A'
                    THEN ISNULL(Num_of_days,0) ELSE 0 END),0)
    -
    ISNULL(SUM(CASE WHEN c_d_code = 2 AND ISNULL(Status,'') = 'A'
                    THEN ISNULL(Num_of_days,0) ELSE 0 END),0)
FROM dbo.leavtrn
WHERE empl = @EmployeeCode
  AND sact_from_date < @SanctionFrom;";

                    AddParameter(openingCommand, "@EmployeeCode", employeeCode.Value);
                    AddParameter(openingCommand, "@SanctionFrom", sanctionFrom.Value.Date);

                    object? result = await openingCommand.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                        openingBalance = Convert.ToDouble(result);
                }

                model.OpeningBalance = openingBalance;

                // =================================================
                // CREATE TRANSACTION COMMAND
                // =================================================
                using DbCommand command =
                    connection.CreateCommand();


                // =================================================
                // LEAVE STATEMENT QUERY
                //
                // IMPORTANT:
                //
                // Show all transactions whose Sanction From and Sanction To
                // dates fall inside the selected date period.
                //
                // Example:
                //
                // From = 08-09-2026
                // To   = 31-12-2027
                //
                // BOTH transaction dates must be inside the selected
                // Sanction From / Sanction To period.
                //
                // Example selected period: 01-01-2026 to 31-12-2026
                // 01-01-2026 -> 31-12-2026 : shown
                // 07-09-2026 -> 11-09-2026 : shown
                // 15-09-2026 -> 16-09-2026 : shown
                // 31-12-2026 -> 31-12-2026 : shown
                //
                // YEAR(sact_from_date) filter is NOT used.
                // =================================================

                command.CommandText = @"
SELECT

    sact_from_date,

    sact_to_date,

    c_d_code,

    sact_code,

    dmnd_from_date,

    dmnd_to_date,

    dmnd_code,

    prv_days,

    Num_of_days,

    sact_officer,

    reason,

    reason_Remark,

    cherge_to,

    effect_pay_date,

    Leave_App_ID,

    Operator,

    Status

FROM dbo.leavtrn

WHERE empl = @EmployeeCode

  AND CAST(sact_from_date AS date)
      BETWEEN @SanctionFrom AND @SanctionTo

  AND CAST(ISNULL(sact_to_date, sact_from_date) AS date)
      BETWEEN @SanctionFrom AND @SanctionTo

ORDER BY

    sact_from_date,

    Leave_App_ID;";


                // =================================================
                // EMPLOYEE CODE PARAMETER
                // =================================================

                AddParameter(
                    command,
                    "@EmployeeCode",
                    employeeCode.Value);


                // =================================================
                // SANCTION FROM PARAMETER
                // =================================================

                AddParameter(
                    command,
                    "@SanctionFrom",
                    sanctionFrom.Value.Date);


                // =================================================
                // SANCTION TO PARAMETER
                // =================================================

                AddParameter(
                    command,
                    "@SanctionTo",
                    sanctionTo.Value.Date);


                // =================================================
                // EXECUTE QUERY
                // =================================================

                using DbDataReader reader =
                    await command
                        .ExecuteReaderAsync();


                // =================================================
                // RUNNING LEAVE BALANCE
                // =================================================

                double runningBalance =
                    openingBalance;


                // =================================================
                // READ TRANSACTIONS
                // =================================================

                while (
                    await reader.ReadAsync())
                {
                    // =============================================
                    // CREDIT / DEBIT CODE
                    // =============================================

                    short? cdCode =
                        reader["c_d_code"] ==
                        DBNull.Value

                            ? null

                            : Convert.ToInt16(
                                reader[
                                    "c_d_code"]);


                    // =============================================
                    // NUMBER OF DAYS
                    // =============================================

                    double days =
                        reader["Num_of_days"] ==
                        DBNull.Value

                            ? 0

                            : Convert.ToDouble(
                                reader[
                                    "Num_of_days"]);


                    // =============================================
                    // STATUS
                    // =============================================

                    string? status =
                        reader["Status"] ==
                        DBNull.Value

                            ? null

                            : Convert.ToString(
                                reader[
                                    "Status"]);


                    // =============================================
                    // DEFAULT CREDIT / DEBIT
                    // =============================================

                    double creditDays =
                        0;

                    double debitDays =
                        0;


                    // =============================================
                    // ONLY APPROVED RECORDS AFFECT BALANCE
                    //
                    // A = APPROVED
                    // =============================================

                    if (string.Equals(
                        status,
                        "A",
                        StringComparison
                            .OrdinalIgnoreCase))
                    {
                        // =========================================
                        // CREDIT
                        // c_d_code = 1
                        // =========================================

                        if (cdCode == 1)
                        {
                            creditDays =
                                days;
                        }


                        // =========================================
                        // DEBIT
                        // c_d_code = 2
                        // =========================================

                        if (cdCode == 2)
                        {
                            debitDays =
                                days;
                        }
                    }


                    // =============================================
                    // RUNNING BALANCE
                    // =============================================

                    double rowOpeningBalance =
                        runningBalance;

                    runningBalance +=
                        creditDays;

                    runningBalance -=
                        debitDays;


                    // =============================================
                    // ADD TRANSACTION TO MODEL
                    // =============================================

                    model.Transactions.Add(
                        new LeaveStatementTransactionViewModel
                        {
                            // -------------------------------------
                            // SANCTION FROM
                            // -------------------------------------

                            SanctionFrom =
                                Convert.ToDateTime(
                                    reader[
                                        "sact_from_date"]),


                            // -------------------------------------
                            // SANCTION TO
                            // -------------------------------------

                            SanctionTo =
                                reader[
                                    "sact_to_date"] ==
                                DBNull.Value

                                    ? null

                                    : Convert.ToDateTime(
                                        reader[
                                            "sact_to_date"]),


                            // -------------------------------------
                            // CREDIT / DEBIT CODE
                            // -------------------------------------

                            CreditDebitCode =
                                cdCode,


                            // -------------------------------------
                            // SANCTION CODE
                            // -------------------------------------

                            SanctionCode =
                                Convert.ToInt32(
                                    reader[
                                        "sact_code"]),


                            // -------------------------------------
                            // DEMAND FROM
                            // -------------------------------------

                            DemandFrom =
                                reader[
                                    "dmnd_from_date"] ==
                                DBNull.Value

                                    ? null

                                    : Convert.ToDateTime(
                                        reader[
                                            "dmnd_from_date"]),


                            // -------------------------------------
                            // DEMAND TO
                            // -------------------------------------

                            DemandTo =
                                reader[
                                    "dmnd_to_date"] ==
                                DBNull.Value

                                    ? null

                                    : Convert.ToDateTime(
                                        reader[
                                            "dmnd_to_date"]),


                            // -------------------------------------
                            // DEMAND CODE
                            // -------------------------------------

                            DemandCode =
                                reader[
                                    "dmnd_code"] ==
                                DBNull.Value

                                    ? null

                                    : Convert.ToInt32(
                                        reader[
                                            "dmnd_code"]),


                            // -------------------------------------
                            // PREVIOUS DAYS
                            // -------------------------------------

                            PreviousDays =
                                reader[
                                    "prv_days"] ==
                                DBNull.Value

                                    ? null

                                    : Convert.ToDouble(
                                        reader[
                                            "prv_days"]),


                            // -------------------------------------
                            // NUMBER OF DAYS
                            // -------------------------------------

                            NumberOfDays =
                                days,


                            // -------------------------------------
                            // SANCTION OFFICER
                            // -------------------------------------

                            SanctionOfficer =
                                reader[
                                    "sact_officer"] ==
                                DBNull.Value

                                    ? null

                                    : Convert.ToInt32(
                                        reader[
                                            "sact_officer"]),


                            // -------------------------------------
                            // REASON
                            // -------------------------------------

                            Reason =
                                reader[
                                    "reason"] ==
                                DBNull.Value

                                    ? null

                                    : Convert.ToInt32(
                                        reader[
                                            "reason"]),


                            // -------------------------------------
                            // REASON REMARK
                            // -------------------------------------

                            ReasonRemark =
                                reader[
                                    "reason_Remark"] ==
                                DBNull.Value

                                    ? null

                                    : Convert.ToString(
                                        reader[
                                            "reason_Remark"]),


                            // -------------------------------------
                            // CHARGE TO
                            // -------------------------------------

                            ChargeTo =
                                reader[
                                    "cherge_to"] ==
                                DBNull.Value

                                    ? null

                                    : Convert.ToInt32(
                                        reader[
                                            "cherge_to"]),


                            // -------------------------------------
                            // EFFECT PAY DATE
                            // -------------------------------------

                            EffectPayDate =
                                reader[
                                    "effect_pay_date"] ==
                                DBNull.Value

                                    ? null

                                    : Convert.ToDateTime(
                                        reader[
                                            "effect_pay_date"]),


                            // -------------------------------------
                            // LEAVE APP ID
                            // -------------------------------------

                            LeaveAppId =
                                reader[
                                    "Leave_App_ID"] ==
                                DBNull.Value

                                    ? null

                                    : Convert.ToDecimal(
                                        reader[
                                            "Leave_App_ID"]),


                            // -------------------------------------
                            // OPERATOR
                            // -------------------------------------

                            Operator =
                                reader[
                                    "Operator"] ==
                                DBNull.Value

                                    ? null

                                    : Convert.ToString(
                                        reader[
                                            "Operator"]),


                            // -------------------------------------
                            // STATUS
                            // -------------------------------------

                            Status =
                                status,


                            // -------------------------------------
                            // CREDIT DAYS
                            // -------------------------------------

                            OpeningBalance =
                                rowOpeningBalance,

                            CreditDays =
                                creditDays,


                            // -------------------------------------
                            // DEBIT DAYS
                            // -------------------------------------

                            DebitDays =
                                debitDays,


                            // -------------------------------------
                            // BALANCE DAYS
                            // -------------------------------------

                            BalanceDays =
                                runningBalance
                        });
                }


                // =================================================
                // OVERALL LEAVE SUMMARY
                //
                // IMPORTANT:
                // These values are NOT restricted by Sanction From /
                // Sanction To. They show the employee's overall
                // approved leave Credit, Debit and Balance.
                //
                // The transaction table above remains date filtered.
                // =================================================

                using (DbCommand overallCommand =
                    connection.CreateCommand())
                {
                    overallCommand.CommandText = @"
SELECT
    ISNULL(SUM(
        CASE
            WHEN c_d_code = 1
             AND ISNULL(Status, '') = 'A'
            THEN ISNULL(Num_of_days, 0)
            ELSE 0
        END
    ), 0) AS TotalCredit,

    ISNULL(SUM(
        CASE
            WHEN c_d_code = 2
             AND ISNULL(Status, '') = 'A'
            THEN ISNULL(Num_of_days, 0)
            ELSE 0
        END
    ), 0) AS TotalDebit

FROM dbo.leavtrn

WHERE empl = @EmployeeCode;";

                    AddParameter(
                        overallCommand,
                        "@EmployeeCode",
                        employeeCode.Value);

                    using DbDataReader overallReader =
                        await overallCommand.ExecuteReaderAsync();

                    if (await overallReader.ReadAsync())
                    {
                        double overallCredit =
                            overallReader["TotalCredit"] == DBNull.Value
                                ? 0
                                : Convert.ToDouble(
                                    overallReader["TotalCredit"]);

                        double overallDebit =
                            overallReader["TotalDebit"] == DBNull.Value
                                ? 0
                                : Convert.ToDouble(
                                    overallReader["TotalDebit"]);

                        model.TotalCredit =
                            overallCredit;

                        model.TotalDebit =
                            overallDebit;

                        model.LeaveBalance =
                            overallCredit -
                            overallDebit;
                    }
                    else
                    {
                        model.TotalCredit = 0;
                        model.TotalDebit = 0;
                        model.LeaveBalance = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                // =================================================
                // ERROR
                // =================================================

                model.ErrorMessage =
                    "Unable to load leave statement. " +
                    ex.Message;
            }
            finally
            {
                // =================================================
                // CLOSE CONNECTION
                // =================================================

                if (closeConnection &&
                    connection.State ==
                    ConnectionState.Open)
                {
                    await connection
                        .CloseAsync();
                }
            }


            // =====================================================
            // RETURN VIEW
            // =====================================================

            return View(
                "Index",
                model);
        }


        // =========================================================
        // SESSION CHECK
        // =========================================================
        private bool IsLoggedIn()
        {
            return
                !string.IsNullOrWhiteSpace(
                    HttpContext.Session
                        .GetString(
                            "LoginId"));
        }


        // =========================================================
        // SQL PARAMETER
        // =========================================================
        private static void AddParameter(
            DbCommand command,
            string name,
            object? value)
        {
            DbParameter parameter =
                command.CreateParameter();

            parameter.ParameterName =
                name;

            parameter.Value =
                value ??
                DBNull.Value;

            command.Parameters.Add(
                parameter);
        }
    }
}