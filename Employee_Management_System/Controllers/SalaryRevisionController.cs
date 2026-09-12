using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace Employee_Management_System.Controllers
{
    public class SalaryRevisionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SalaryRevisionController> _logger;


        public SalaryRevisionController(
            ApplicationDbContext context,
            ILogger<SalaryRevisionController> logger)
        {
            _context = context;
            _logger = logger;
        }


        // =========================================================
        // INDEX
        // =========================================================

        [HttpGet]
        public IActionResult Index()
        {
            var model = new SalaryRevisionViewModel
            {
                FromDate = DateTime.Today,
                ToDate = new DateTime(2035, 3, 31)
            };

            return View(model);
        }


        // =========================================================
        // SEARCH EMPLOYEE
        //
        // Employee Code -> dbo.PayMast.code
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


                var connection =
                    _context.Database.GetDbConnection();


                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }


                await using var command =
                    connection.CreateCommand();


                command.CommandText = @"
                    SELECT TOP 1
                        code,
                        name,
                        basic,
                        last_incr_date
                    FROM dbo.PayMast
                    WHERE code = @EmployeeCode;
                ";


                AddParameter(
                    command,
                    "@EmployeeCode",
                    employeeCode
                );


                await using var reader =
                    await command.ExecuteReaderAsync();


                if (!await reader.ReadAsync())
                {
                    return Json(new
                    {
                        success = false,
                        message = "Employee Code not found in PayMast."
                    });
                }


                string employeeName =
                    reader["name"] == DBNull.Value
                        ? ""
                        : Convert.ToString(
                            reader["name"]
                        ) ?? "";


                double currentBasic =
                    reader["basic"] == DBNull.Value
                        ? 0
                        : Convert.ToDouble(
                            reader["basic"]
                        );


                string? lastIncrementDate = null;


                if (
                    reader["last_incr_date"]
                    != DBNull.Value
                )
                {
                    DateTime date =
                        Convert.ToDateTime(
                            reader["last_incr_date"]
                        );


                    lastIncrementDate =
                        date.ToString("yyyy-MM-dd");
                }


                return Json(new
                {
                    success = true,

                    data = new
                    {
                        employeeCode,
                        employeeName,
                        currentBasic,
                        lastIncrementDate
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Employee Search Error. Employee Code: {EmployeeCode}",
                    employeeCode
                );


                return StatusCode(
                    500,
                    new
                    {
                        success = false,
                        message =
                            "Unable to retrieve employee information."
                    }
                );
            }
        }


        // =========================================================
        // SAVE SALARY REVISION
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(
            SalaryRevisionViewModel model)
        {
            DbTransaction? transaction = null;


            try
            {
                // =================================================
                // VALIDATION
                // =================================================

                if (
                    !model.EmployeeCode.HasValue
                    ||
                    model.EmployeeCode.Value <= 0
                )
                {
                    TempData["Error"] =
                        "Please search Employee Code first.";

                    return RedirectToAction(
                        nameof(Index)
                    );
                }


                if (
                    string.IsNullOrWhiteSpace(
                        model.RevisionType
                    )
                )
                {
                    TempData["Error"] =
                        "Please select Increment or Decrement.";

                    return RedirectToAction(
                        nameof(Index)
                    );
                }


                string revisionType =
                    model.RevisionType.Trim();


                if (
                    !revisionType.Equals(
                        "Increment",
                        StringComparison.OrdinalIgnoreCase
                    )
                    &&
                    !revisionType.Equals(
                        "Decrement",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    TempData["Error"] =
                        "Invalid Revision Type.";

                    return RedirectToAction(
                        nameof(Index)
                    );
                }


                if (model.RevisionAmount <= 0)
                {
                    TempData["Error"] =
                        "Revision Amount must be greater than zero.";

                    return RedirectToAction(
                        nameof(Index)
                    );
                }


                if (model.FromDate == DateTime.MinValue)
                {
                    TempData["Error"] =
                        "Effective From Date is required.";

                    return RedirectToAction(
                        nameof(Index)
                    );
                }


                if (model.ToDate == DateTime.MinValue)
                {
                    TempData["Error"] =
                        "Effective To Date is required.";

                    return RedirectToAction(
                        nameof(Index)
                    );
                }


                if (model.ToDate < model.FromDate)
                {
                    TempData["Error"] =
                        "Effective To Date cannot be before From Date.";

                    return RedirectToAction(
                        nameof(Index)
                    );
                }


                int employeeCode =
                    model.EmployeeCode.Value;


                var connection =
                    _context.Database.GetDbConnection();


                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }


                transaction =
                    await connection.BeginTransactionAsync();


                // =================================================
                // READ CURRENT EMPLOYEE DATA FROM PayMast
                // =================================================

                string employeeName;

                double currentBasic;


                await using (
                    var employeeCommand =
                        connection.CreateCommand()
                )
                {
                    employeeCommand.Transaction =
                        transaction;


                    employeeCommand.CommandText = @"
                        SELECT TOP 1
                            name,
                            basic
                        FROM dbo.PayMast
                        WHERE code = @EmployeeCode;
                    ";


                    AddParameter(
                        employeeCommand,
                        "@EmployeeCode",
                        employeeCode
                    );


                    await using var reader =
                        await employeeCommand
                            .ExecuteReaderAsync();


                    if (!await reader.ReadAsync())
                    {
                        await transaction.RollbackAsync();


                        TempData["Error"] =
                            "Employee Code not found in PayMast.";


                        return RedirectToAction(
                            nameof(Index)
                        );
                    }


                    employeeName =
                        reader["name"] == DBNull.Value
                            ? ""
                            : Convert.ToString(
                                reader["name"]
                            ) ?? "";


                    if (
                        reader["basic"]
                        ==
                        DBNull.Value
                    )
                    {
                        await transaction.RollbackAsync();


                        TempData["Error"] =
                            "Basic Salary not found for this Employee.";


                        return RedirectToAction(
                            nameof(Index)
                        );
                    }


                    currentBasic =
                        Convert.ToDouble(
                            reader["basic"]
                        );
                }


                // =================================================
                // CALCULATE NEW BASIC
                // =================================================

                double revisionAmount =
                    model.RevisionAmount;


                double newBasic;


                if (
                    revisionType.Equals(
                        "Increment",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    newBasic =
                        currentBasic
                        +
                        revisionAmount;
                }
                else
                {
                    newBasic =
                        currentBasic
                        -
                        revisionAmount;
                }


                if (newBasic < 0)
                {
                    await transaction.RollbackAsync();


                    TempData["Error"] =
                        "Decrement Amount cannot be greater than Current Basic.";


                    return RedirectToAction(
                        nameof(Index)
                    );
                }


                // =================================================
                // GENERATE NEXT SRNO
                // =================================================

                int nextSrNo;


                await using (
                    var srnoCommand =
                        connection.CreateCommand()
                )
                {
                    srnoCommand.Transaction =
                        transaction;


                    srnoCommand.CommandText = @"
                        SELECT
                            ISNULL(
                                MAX(CAST(srno AS INT)),
                                0
                            )
                        FROM dbo.incrtrn
                        WITH (UPDLOCK, HOLDLOCK);
                    ";


                    object? result =
                        await srnoCommand
                            .ExecuteScalarAsync();


                    nextSrNo =
                        result == null
                        ||
                        result == DBNull.Value

                            ? 1

                            : Convert.ToInt32(
                                result
                            ) + 1;
                }


                if (nextSrNo > short.MaxValue)
                {
                    await transaction.RollbackAsync();


                    TempData["Error"] =
                        "SRNO limit exceeded.";


                    return RedirectToAction(
                        nameof(Index)
                    );
                }


                // =================================================
                // LEGACY VALUES CURRENTLY USED BY THIS MODULE
                // =================================================

                short cdCode = 1;

                int nos = 0;


                short addFlag =
                    revisionType.Equals(
                        "Increment",
                        StringComparison.OrdinalIgnoreCase
                    )
                        ? (short)1
                        : (short)0;


                // =================================================
                // INSERT INTO incrtrn
                // =================================================

                await using (
                    var insertCommand =
                        connection.CreateCommand()
                )
                {
                    insertCommand.Transaction =
                        transaction;


                    insertCommand.CommandText = @"
                        INSERT INTO dbo.incrtrn
                        (
                            [year],
                            [month],
                            empl,
                            srno,
                            c_d_code,
                            nos,
                            amt,
                            prv_amt,
                            from_date,
                            to_date,
                            add_flag
                        )
                        VALUES
                        (
                            @Year,
                            @Month,
                            @EmployeeCode,
                            @SrNo,
                            @CDCode,
                            @Nos,
                            @Amount,
                            @PreviousBasic,
                            @FromDate,
                            @ToDate,
                            @AddFlag
                        );
                    ";


                    AddParameter(
                        insertCommand,
                        "@Year",
                        model.FromDate.Year
                    );


                    AddParameter(
                        insertCommand,
                        "@Month",
                        model.FromDate.Month
                    );


                    AddParameter(
                        insertCommand,
                        "@EmployeeCode",
                        employeeCode
                    );


                    AddParameter(
                        insertCommand,
                        "@SrNo",
                        nextSrNo
                    );


                    AddParameter(
                        insertCommand,
                        "@CDCode",
                        cdCode
                    );


                    AddParameter(
                        insertCommand,
                        "@Nos",
                        nos
                    );


                    AddParameter(
                        insertCommand,
                        "@Amount",
                        revisionAmount
                    );


                    AddParameter(
                        insertCommand,
                        "@PreviousBasic",
                        currentBasic
                    );


                    AddParameter(
                        insertCommand,
                        "@FromDate",
                        model.FromDate
                    );


                    AddParameter(
                        insertCommand,
                        "@ToDate",
                        model.ToDate
                    );


                    AddParameter(
                        insertCommand,
                        "@AddFlag",
                        addFlag
                    );


                    int inserted =
                        await insertCommand
                            .ExecuteNonQueryAsync();


                    if (inserted != 1)
                    {
                        throw new Exception(
                            "Unable to insert Salary Revision into incrtrn."
                        );
                    }
                }


                // =================================================
                // UPDATE PayMast
                // =================================================

                await using (
                    var updateCommand =
                        connection.CreateCommand()
                )
                {
                    updateCommand.Transaction =
                        transaction;


                    if (
                        revisionType.Equals(
                            "Increment",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        updateCommand.CommandText = @"
                            UPDATE dbo.PayMast

                            SET
                                basic = @NewBasic,
                                last_incr_date = @FromDate

                            WHERE code = @EmployeeCode;
                        ";


                        AddParameter(
                            updateCommand,
                            "@FromDate",
                            model.FromDate
                        );
                    }
                    else
                    {
                        updateCommand.CommandText = @"
                            UPDATE dbo.PayMast

                            SET
                                basic = @NewBasic

                            WHERE code = @EmployeeCode;
                        ";
                    }


                    AddParameter(
                        updateCommand,
                        "@NewBasic",
                        newBasic
                    );


                    AddParameter(
                        updateCommand,
                        "@EmployeeCode",
                        employeeCode
                    );


                    int updated =
                        await updateCommand
                            .ExecuteNonQueryAsync();


                    if (updated != 1)
                    {
                        throw new Exception(
                            "Unable to update PayMast."
                        );
                    }
                }


                // =================================================
                // COMMIT
                // =================================================

                await transaction.CommitAsync();


                TempData["Success"] =
                    "Salary Revision saved successfully.";


                TempData["EmployeeCode"] =
                    employeeCode.ToString();


                TempData["EmployeeName"] =
                    employeeName;


                TempData["RevisionType"] =
                    revisionType;


                TempData["PreviousBasic"] =
                    currentBasic.ToString("0.00");


                TempData["RevisionAmount"] =
                    revisionAmount.ToString("0.00");


                TempData["NewBasic"] =
                    newBasic.ToString("0.00");


                TempData["SrNo"] =
                    nextSrNo.ToString();


                return RedirectToAction(
                    nameof(Index)
                );
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    try
                    {
                        await transaction.RollbackAsync();
                    }
                    catch
                    {
                    }
                }


                _logger.LogError(
                    ex,
                    "Salary Revision Save Error"
                );


                TempData["Error"] =
                    "Unable to save Salary Revision.";


                return RedirectToAction(
                    nameof(Index)
                );
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }


        // =========================================================
        // PARAMETER HELPER
        // =========================================================

        private static void AddParameter(
            DbCommand command,
            string name,
            object? value)
        {
            var parameter =
                command.CreateParameter();


            parameter.ParameterName =
                name;


            parameter.Value =
                value ?? DBNull.Value;


            command.Parameters.Add(
                parameter
            );
        }
    }
}