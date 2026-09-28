using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace Employee_Management_System.Controllers
{
    public class LeaveCreditController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LeaveCreditController(
            ApplicationDbContext context)
        {
            _context = context;
        }


        // =========================================================
        // INDEX
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            var model =
                new LeaveCreditViewModel
                {
                    SelectedYear =
                        DateTime.Now.Year,

                    LeaveTypes =
                        await LoadLeaveTypes(),

                    Branches =
                        await LoadBranches()
                };

            return View(model);
        }


        // =========================================================
        // LOAD LEAVE TYPES
        // TABLE: dbo.leavmast
        // =========================================================
        private async Task<List<SelectListItem>>
            LoadLeaveTypes()
        {
            var result =
                new List<SelectListItem>();

            DbConnection connection =
                _context.Database.GetDbConnection();

            bool shouldClose =
                connection.State !=
                ConnectionState.Open;

            try
            {
                if (shouldClose)
                {
                    await connection.OpenAsync();
                }

                using DbCommand command =
                    connection.CreateCommand();

                command.CommandText = @"
SELECT
    code,
    name
FROM dbo.leavmast
WHERE ISNULL(Active, 'N') = 'Y'
ORDER BY code;
";

                using DbDataReader reader =
                    await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    short code =
                        Convert.ToInt16(
                            reader["code"]
                        );

                    string name =
                        reader["name"] ==
                        DBNull.Value
                            ? ""
                            : Convert.ToString(
                                reader["name"]
                              )?.Trim() ?? "";

                    result.Add(
                        new SelectListItem
                        {
                            Value =
                                code.ToString(),

                            Text =
                                code +
                                " - " +
                                name
                        }
                    );
                }
            }
            finally
            {
                if (shouldClose &&
                    connection.State ==
                    ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }

            return result;
        }


        // =========================================================
        // LOAD BRANCHES
        // TABLES:
        // dbo.brncmast
        // dbo.PayMast
        // =========================================================
        private async Task<
            List<LeaveCreditBranchViewModel>>
            LoadBranches()
        {
            var result =
                new List<
                    LeaveCreditBranchViewModel>();

            DbConnection connection =
                _context.Database.GetDbConnection();

            bool shouldClose =
                connection.State !=
                ConnectionState.Open;

            try
            {
                if (shouldClose)
                {
                    await connection.OpenAsync();
                }

                using DbCommand command =
                    connection.CreateCommand();

                command.CommandText = @"
SELECT
    CONVERT(INT, b.code)
        AS BranchCode,

    b.name
        AS BranchName,

    COUNT(p.code)
        AS EmployeeCount

FROM dbo.brncmast b

LEFT JOIN dbo.PayMast p
    ON TRY_CONVERT(
        INT,
        p.brnc_code
    )
    =
    TRY_CONVERT(
        INT,
        b.code
    )

GROUP BY
    b.code,
    b.name

ORDER BY
    TRY_CONVERT(
        INT,
        b.code
    );
";

                using DbDataReader reader =
                    await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Add(
                        new LeaveCreditBranchViewModel
                        {
                            BranchCode =
                                Convert.ToInt32(
                                    reader[
                                        "BranchCode"
                                    ]
                                ),

                            BranchName =
                                reader[
                                    "BranchName"
                                ] == DBNull.Value
                                    ? ""
                                    : Convert
                                        .ToString(
                                            reader[
                                                "BranchName"
                                            ]
                                        )
                                        ?.Trim(),

                            EmployeeCount =
                                Convert.ToInt32(
                                    reader[
                                        "EmployeeCount"
                                    ]
                                ),

                            SelectedEmployeeCount =
                                0
                        }
                    );
                }
            }
            finally
            {
                if (shouldClose &&
                    connection.State ==
                    ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }

            return result;
        }


        // =========================================================
        // GET LEAVE LIMIT
        //
        // dbo.leavmast.leave_limit
        // numeric(18,0)
        //
        // RULE:
        // leave_limit > 0 = Maximum limit
        // leave_limit = 0 = No maximum configured
        // leave_limit NULL = No maximum configured
        // =========================================================
        [HttpGet]
        public async Task<IActionResult>
            GetLeaveLimit(
                short leaveCode)
        {
            if (!IsLoggedIn())
            {
                return Json(new
                {
                    success = false,

                    sessionExpired = true,

                    message =
                        "Login session expired."
                });
            }

            if (leaveCode <= 0)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Please select Leave Type."
                });
            }

            try
            {
                DbConnection connection =
                    _context.Database
                        .GetDbConnection();

                bool shouldClose =
                    connection.State !=
                    ConnectionState.Open;

                try
                {
                    if (shouldClose)
                    {
                        await connection
                            .OpenAsync();
                    }

                    using DbCommand command =
                        connection
                            .CreateCommand();

                    command.CommandText = @"
SELECT TOP 1

    code,

    ISNULL(name, '')
        AS LeaveName,

    leave_limit

FROM dbo.leavmast

WHERE
    code = @LeaveCode;
";

                    AddParameter(
                        command,
                        "@LeaveCode",
                        leaveCode
                    );

                    using DbDataReader reader =
                        await command
                            .ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        return Json(new
                        {
                            success = false,

                            message =
                                "Selected Leave Type not found."
                        });
                    }

                    string leaveName =
                        Convert.ToString(
                            reader[
                                "LeaveName"
                            ]
                        )?.Trim() ?? "";

                    decimal? leaveLimit =
                        null;

                    if (reader["leave_limit"] !=
                        DBNull.Value)
                    {
                        leaveLimit =
                            Convert.ToDecimal(
                                reader[
                                    "leave_limit"
                                ]
                            );
                    }

                    return Json(new
                    {
                        success = true,

                        leaveCode =
                            leaveCode,

                        leaveName =
                            leaveName,

                        leaveLimit =
                            leaveLimit,

                        hasLimit =
                            leaveLimit.HasValue &&
                            leaveLimit.Value > 0
                    });
                }
                finally
                {
                    if (shouldClose &&
                        connection.State ==
                        ConnectionState.Open)
                    {
                        await connection
                            .CloseAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Unable to retrieve " +
                        "Leave Limit. " +
                        ex.Message
                });
            }
        }


        // =========================================================
        // GET BRANCH SELECTED COUNTS
        // =========================================================
        [HttpGet]
        public async Task<IActionResult>
            GetBranchSelectedCounts(
                int year,
                short leaveCode)
        {
            if (!IsLoggedIn())
            {
                return Json(new
                {
                    success = false,

                    sessionExpired = true,

                    message =
                        "Login session expired."
                });
            }

            if (year <= 0 ||
                leaveCode <= 0)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Invalid Year or Leave Type."
                });
            }

            try
            {
                DateTime fromDate =
                    new DateTime(
                        year,
                        1,
                        1
                    );

                DateTime toDate =
                    new DateTime(
                        year,
                        12,
                        31
                    );

                var result =
                    new List<object>();

                DbConnection connection =
                    _context.Database
                        .GetDbConnection();

                bool shouldClose =
                    connection.State !=
                    ConnectionState.Open;

                try
                {
                    if (shouldClose)
                    {
                        await connection
                            .OpenAsync();
                    }

                    using DbCommand command =
                        connection
                            .CreateCommand();

                    command.CommandText = @"
SELECT

    CONVERT(
        INT,
        b.code
    ) AS BranchCode,

    COUNT(
        DISTINCT
        CASE
            WHEN l.empl IS NOT NULL
            THEN p.code
        END
    ) AS SelectedCount

FROM dbo.brncmast b

LEFT JOIN dbo.PayMast p
    ON TRY_CONVERT(
        INT,
        p.brnc_code
    )
    =
    TRY_CONVERT(
        INT,
        b.code
    )

LEFT JOIN dbo.leavtrn l
    ON l.empl = p.code

    AND l.sact_from_date =
        @FromDate

    AND l.sact_to_date =
        @ToDate

    AND l.sact_code =
        @LeaveCode

    AND l.dmnd_code =
        @LeaveCode

    AND l.c_d_code = 1

    AND UPPER(
        LTRIM(
            RTRIM(
                ISNULL(
                    l.Status,
                    ''
                )
            )
        )
    ) = 'A'

GROUP BY
    b.code

ORDER BY
    TRY_CONVERT(
        INT,
        b.code
    );
";

                    AddParameter(
                        command,
                        "@FromDate",
                        fromDate
                    );

                    AddParameter(
                        command,
                        "@ToDate",
                        toDate
                    );

                    AddParameter(
                        command,
                        "@LeaveCode",
                        leaveCode
                    );

                    using DbDataReader reader =
                        await command
                            .ExecuteReaderAsync();

                    while (
                        await reader.ReadAsync()
                    )
                    {
                        result.Add(
                            new
                            {
                                branchCode =
                                    Convert.ToInt32(
                                        reader[
                                            "BranchCode"
                                        ]
                                    ),

                                selectedCount =
                                    Convert.ToInt32(
                                        reader[
                                            "SelectedCount"
                                        ]
                                    )
                            }
                        );
                    }
                }
                finally
                {
                    if (shouldClose &&
                        connection.State ==
                        ConnectionState.Open)
                    {
                        await connection
                            .CloseAsync();
                    }
                }

                return Json(new
                {
                    success = true,

                    data = result
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Unable to load branch " +
                        "selected counts. " +
                        ex.Message
                });
            }
        }


        // =========================================================
        // GET EMPLOYEES BY BRANCH
        // =========================================================
        [HttpGet]
        public async Task<IActionResult>
            GetEmployeesByBranch(
                int branchCode,
                int year,
                short leaveCode)
        {
            if (!IsLoggedIn())
            {
                return Json(new
                {
                    success = false,

                    sessionExpired = true,

                    message =
                        "Login session expired."
                });
            }

            if (branchCode <= 0)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Invalid Branch Code."
                });
            }

            if (year <= 0)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Please select Year."
                });
            }

            if (leaveCode <= 0)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Please select Leave Type."
                });
            }

            try
            {
                DateTime fromDate =
                    new DateTime(
                        year,
                        1,
                        1
                    );

                DateTime toDate =
                    new DateTime(
                        year,
                        12,
                        31
                    );

                var employees =
                    new List<
                        LeaveCreditEmployeeViewModel>();

                DbConnection connection =
                    _context.Database
                        .GetDbConnection();

                bool shouldClose =
                    connection.State !=
                    ConnectionState.Open;

                try
                {
                    if (shouldClose)
                    {
                        await connection
                            .OpenAsync();
                    }

                    using DbCommand command =
                        connection
                            .CreateCommand();

                    command.CommandText = @"
SELECT

    p.code
        AS EmployeeCode,

    ISNULL(
        p.name,
        ''
    ) AS EmployeeName,

    CASE

        WHEN EXISTS
        (
            SELECT 1

            FROM dbo.leavtrn l

            WHERE
                l.empl =
                    p.code

                AND
                l.sact_from_date =
                    @FromDate

                AND
                l.sact_to_date =
                    @ToDate

                AND
                l.sact_code =
                    @LeaveCode

                AND
                l.dmnd_code =
                    @LeaveCode

                AND
                l.c_d_code = 1

                AND
                UPPER(
                    LTRIM(
                        RTRIM(
                            ISNULL(
                                l.Status,
                                ''
                            )
                        )
                    )
                ) = 'A'
        )

        THEN 1

        ELSE 0

    END AS IsSelected

FROM dbo.PayMast p

WHERE

    TRY_CONVERT(
        INT,
        p.brnc_code
    ) =
        @BranchCode

    AND
    (
        NOT EXISTS
        (
            SELECT 1 FROM dbo.leavmast lm
            WHERE lm.code = @LeaveCode
              AND UPPER(ISNULL(lm.name, '')) LIKE '%MATERNITY%'
        )
        OR UPPER(LTRIM(RTRIM(ISNULL(p.sex, '')))) IN ('F', 'FEMALE')
    )

ORDER BY
    p.code;
";

                    AddParameter(
                        command,
                        "@FromDate",
                        fromDate
                    );

                    AddParameter(
                        command,
                        "@ToDate",
                        toDate
                    );

                    AddParameter(
                        command,
                        "@LeaveCode",
                        leaveCode
                    );

                    AddParameter(
                        command,
                        "@BranchCode",
                        branchCode
                    );

                    using DbDataReader reader =
                        await command
                            .ExecuteReaderAsync();

                    while (
                        await reader.ReadAsync()
                    )
                    {
                        employees.Add(
                            new LeaveCreditEmployeeViewModel
                            {
                                EmployeeCode =
                                    Convert.ToInt32(
                                        reader[
                                            "EmployeeCode"
                                        ]
                                    ),

                                EmployeeName =
                                    Convert.ToString(
                                        reader[
                                            "EmployeeName"
                                        ]
                                    )?.Trim() ?? "",

                                IsSelected =
                                    Convert.ToInt32(
                                        reader[
                                            "IsSelected"
                                        ]
                                    ) == 1
                            }
                        );
                    }
                }
                finally
                {
                    if (shouldClose &&
                        connection.State ==
                        ConnectionState.Open)
                    {
                        await connection
                            .CloseAsync();
                    }
                }

                return Json(new
                {
                    success = true,

                    data =
                        employees
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Unable to load employees. " +
                        ex.Message
                });
            }
        }


        // =========================================================
        // GET ALL EMPLOYEES
        //
        // Used by GLOBAL SELECT ALL
        // =========================================================
        [HttpGet]
        public async Task<IActionResult>
            GetAllEmployees(
                int year,
                short leaveCode)
        {
            if (!IsLoggedIn())
            {
                return Json(new
                {
                    success = false,

                    sessionExpired = true,

                    message =
                        "Login session expired."
                });
            }

            if (year <= 0)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Please select Year."
                });
            }

            if (leaveCode <= 0)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Please select Leave Type."
                });
            }

            try
            {
                DateTime fromDate =
                    new DateTime(
                        year,
                        1,
                        1
                    );

                DateTime toDate =
                    new DateTime(
                        year,
                        12,
                        31
                    );

                var employees =
                    new List<object>();

                DbConnection connection =
                    _context.Database
                        .GetDbConnection();

                bool shouldClose =
                    connection.State !=
                    ConnectionState.Open;

                try
                {
                    if (shouldClose)
                    {
                        await connection
                            .OpenAsync();
                    }

                    using DbCommand command =
                        connection
                            .CreateCommand();

                    command.CommandText = @"
SELECT

    p.code
        AS EmployeeCode,

    ISNULL(
        p.name,
        ''
    ) AS EmployeeName,

    TRY_CONVERT(
        INT,
        p.brnc_code
    ) AS BranchCode,

    CASE

        WHEN EXISTS
        (
            SELECT 1

            FROM dbo.leavtrn l

            WHERE
                l.empl =
                    p.code

                AND
                l.sact_from_date =
                    @FromDate

                AND
                l.sact_to_date =
                    @ToDate

                AND
                l.sact_code =
                    @LeaveCode

                AND
                l.dmnd_code =
                    @LeaveCode

                AND
                l.c_d_code = 1

                AND
                UPPER(
                    LTRIM(
                        RTRIM(
                            ISNULL(
                                l.Status,
                                ''
                            )
                        )
                    )
                ) = 'A'
        )

        THEN 1

        ELSE 0

    END AS IsSelected

FROM dbo.PayMast p

WHERE

    TRY_CONVERT(
        INT,
        p.brnc_code
    ) IS NOT NULL

    AND
    (
        NOT EXISTS
        (
            SELECT 1 FROM dbo.leavmast lm
            WHERE lm.code = @LeaveCode
              AND UPPER(ISNULL(lm.name, '')) LIKE '%MATERNITY%'
        )
        OR UPPER(LTRIM(RTRIM(ISNULL(p.sex, '')))) IN ('F', 'FEMALE')
    )

ORDER BY

    TRY_CONVERT(
        INT,
        p.brnc_code
    ),

    p.code;
";

                    AddParameter(
                        command,
                        "@FromDate",
                        fromDate
                    );

                    AddParameter(
                        command,
                        "@ToDate",
                        toDate
                    );

                    AddParameter(
                        command,
                        "@LeaveCode",
                        leaveCode
                    );

                    using DbDataReader reader =
                        await command
                            .ExecuteReaderAsync();

                    while (
                        await reader.ReadAsync()
                    )
                    {
                        employees.Add(
                            new
                            {
                                employeeCode =
                                    Convert.ToInt32(
                                        reader[
                                            "EmployeeCode"
                                        ]
                                    ),

                                employeeName =
                                    Convert.ToString(
                                        reader[
                                            "EmployeeName"
                                        ]
                                    )?.Trim() ?? "",

                                branchCode =
                                    Convert.ToInt32(
                                        reader[
                                            "BranchCode"
                                        ]
                                    ),

                                isSelected =
                                    Convert.ToInt32(
                                        reader[
                                            "IsSelected"
                                        ]
                                    ) == 1
                            }
                        );
                    }
                }
                finally
                {
                    if (shouldClose &&
                        connection.State ==
                        ConnectionState.Open)
                    {
                        await connection
                            .CloseAsync();
                    }
                }

                return Json(new
                {
                    success = true,

                    data =
                        employees
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Unable to load all employees. " +
                        ex.Message
                });
            }
        }


        // =========================================================
        // SAVE LEAVE CREDIT
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(
            int year,
            short leaveCode,
            double numberOfDays,
            List<int> selectedEmployeeCodes)
        {
            // =====================================================
            // LOGIN ID
            // UserMast.ini -> Session["LoginId"]
            // =====================================================

            string? loginId =
                HttpContext.Session
                    .GetString(
                        "LoginId"
                    );

            if (string.IsNullOrWhiteSpace(
                    loginId))
            {
                return Json(new
                {
                    success = false,

                    sessionExpired = true,

                    message =
                        "Login session expired. " +
                        "Please login again."
                });
            }

            loginId =
                loginId.Trim();


            // =====================================================
            // LOGIN ID VALIDATION
            //
            // sact_officer = INT
            // Operator     = NVARCHAR
            // =====================================================

            if (!int.TryParse(
                    loginId,
                    out int loginUserCode))
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Login ID is invalid. " +
                        "sact_officer requires " +
                        "numeric Login ID."
                });
            }


            // =====================================================
            // BASIC VALIDATION
            // =====================================================

            if (year <= 0)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Please select Year."
                });
            }


            if (leaveCode <= 0)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Please select Leave Type."
                });
            }


            if (numberOfDays <= 0)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Please enter Number of Days."
                });
            }


            // =====================================================
            // SERVER-SIDE LEAVE LIMIT VALIDATION
            //
            // Never depend only on JavaScript.
            // Always read leave_limit from DB before INSERT.
            // =====================================================

            decimal? leaveLimit =
                null;

            string leaveName =
                "";

            try
            {
                DbConnection limitConnection =
                    _context.Database
                        .GetDbConnection();

                bool closeLimitConnection =
                    limitConnection.State !=
                    ConnectionState.Open;

                try
                {
                    if (closeLimitConnection)
                    {
                        await limitConnection
                            .OpenAsync();
                    }

                    using DbCommand limitCommand =
                        limitConnection
                            .CreateCommand();

                    limitCommand.CommandText = @"
SELECT TOP 1

    ISNULL(
        name,
        ''
    ) AS LeaveName,

    leave_limit

FROM dbo.leavmast

WHERE
    code = @LeaveCode;
";

                    AddParameter(
                        limitCommand,
                        "@LeaveCode",
                        leaveCode
                    );

                    using DbDataReader limitReader =
                        await limitCommand
                            .ExecuteReaderAsync();

                    if (!await limitReader
                            .ReadAsync())
                    {
                        return Json(new
                        {
                            success = false,

                            message =
                                "Selected Leave Type " +
                                "not found."
                        });
                    }

                    leaveName =
                        Convert.ToString(
                            limitReader[
                                "LeaveName"
                            ]
                        )?.Trim() ?? "";

                    if (
                        limitReader[
                            "leave_limit"
                        ] != DBNull.Value
                    )
                    {
                        leaveLimit =
                            Convert.ToDecimal(
                                limitReader[
                                    "leave_limit"
                                ]
                            );
                    }
                }
                finally
                {
                    if (closeLimitConnection &&
                        limitConnection.State ==
                        ConnectionState.Open)
                    {
                        await limitConnection
                            .CloseAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Unable to validate " +
                        "Leave Limit. " +
                        ex.Message
                });
            }


            // =====================================================
            // MATERNITY LEAVE FLAG
            // =====================================================
            bool isMaternityLeave =
                !string.IsNullOrWhiteSpace(leaveName) &&
                leaveName.Contains(
                    "MATERNITY",
                    StringComparison.OrdinalIgnoreCase);


            // =====================================================
            // APPLY LEAVE LIMIT
            //
            // leave_limit > 0 = Restrict
            // leave_limit 0   = No maximum configured
            // leave_limit NULL= No maximum configured
            // =====================================================

            if (
                leaveLimit.HasValue &&
                leaveLimit.Value > 0 &&
                Convert.ToDecimal(
                    numberOfDays
                ) >
                leaveLimit.Value
            )
            {
                return Json(new
                {
                    success = false,

                    limitExceeded = true,

                    leaveLimit =
                        leaveLimit.Value,

                    leaveName =
                        leaveName,

                    message =
                        "Maximum leave limit for " +
                        leaveName +
                        " is " +
                        leaveLimit.Value
                            .ToString("0") +
                        " day(s). " +
                        "Please enter Number of Days " +
                        "up to " +
                        leaveLimit.Value
                            .ToString("0") +
                        "."
                });
            }


            // =====================================================
            // EMPLOYEE VALIDATION
            // =====================================================

            if (
                selectedEmployeeCodes == null ||
                selectedEmployeeCodes.Count == 0
            )
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Please select at least " +
                        "one employee."
                });
            }


            // Remove duplicate employee codes

            selectedEmployeeCodes =
                selectedEmployeeCodes
                    .Distinct()
                    .ToList();


            // =====================================================
            // YEAR DATES
            // =====================================================

            DateTime fromDate =
                new DateTime(
                    year,
                    1,
                    1
                );

            DateTime toDate =
                new DateTime(
                    year,
                    12,
                    31
                );


            DbConnection connection =
                _context.Database
                    .GetDbConnection();

            bool shouldClose =
                connection.State !=
                ConnectionState.Open;

            DbTransaction? transaction =
                null;


            try
            {
                if (shouldClose)
                {
                    await connection
                        .OpenAsync();
                }


                transaction =
                    await connection
                        .BeginTransactionAsync();


                // =================================================
                // GENERATE Leave_App_ID
                // =================================================

                decimal leaveAppId;


                using (
                    DbCommand idCommand =
                        connection
                            .CreateCommand()
                )
                {
                    idCommand.Transaction =
                        transaction;

                    idCommand.CommandText = @"
SELECT

    ISNULL(
        MAX(Leave_App_ID),
        0
    ) + 1

FROM dbo.leavtrn;
";

                    object? value =
                        await idCommand
                            .ExecuteScalarAsync();

                    leaveAppId =
                        Convert.ToDecimal(
                            value
                        );
                }


                int insertedCount =
                    0;

                int skippedCount =
                    0;


                // =================================================
                // LOOP ALL SELECTED EMPLOYEES
                // =================================================

                foreach (
                    int employeeCode
                    in selectedEmployeeCodes
                )
                {
                    // =============================================
                    // EMPLOYEE EXISTS?
                    // =============================================

                    bool employeeExists;
                    string employeeGender = "";


                    using (
                        DbCommand checkEmployee =
                            connection
                                .CreateCommand()
                    )
                    {
                        checkEmployee.Transaction =
                            transaction;

                        checkEmployee.CommandText = @"
SELECT TOP 1
    ISNULL(sex, '') AS Gender
FROM dbo.PayMast
WHERE code = @EmployeeCode;
";

                        AddParameter(
                            checkEmployee,
                            "@EmployeeCode",
                            employeeCode
                        );

                        object? genderResult =
                            await checkEmployee.ExecuteScalarAsync();

                        employeeExists =
                            genderResult != null &&
                            genderResult != DBNull.Value;

                        employeeGender =
                            employeeExists
                                ? Convert.ToString(genderResult)?.Trim() ?? ""
                                : "";
                    }


                    if (!employeeExists)
                    {
                        skippedCount++;
                        continue;
                    }

                    if (isMaternityLeave)
                    {
                        bool isFemale =
                            employeeGender.Equals("F", StringComparison.OrdinalIgnoreCase) ||
                            employeeGender.Equals("FEMALE", StringComparison.OrdinalIgnoreCase);

                        if (!isFemale)
                        {
                            skippedCount++;
                            continue;
                        }
                    }


                    // =============================================
                    // DUPLICATE CHECK
                    //
                    // Current leavtrn composite PK:
                    // sact_from_date + empl
                    // =============================================

                    bool exists;


                    using (
                        DbCommand duplicateCommand =
                            connection
                                .CreateCommand()
                    )
                    {
                        duplicateCommand.Transaction =
                            transaction;

                        duplicateCommand.CommandText = @"
SELECT COUNT(1)

FROM dbo.leavtrn

WHERE

    sact_from_date =
        @FromDate

    AND

    empl =
        @EmployeeCode;
";

                        AddParameter(
                            duplicateCommand,
                            "@FromDate",
                            fromDate
                        );

                        AddParameter(
                            duplicateCommand,
                            "@EmployeeCode",
                            employeeCode
                        );

                        exists =
                            Convert.ToInt32(
                                await duplicateCommand
                                    .ExecuteScalarAsync()
                            ) > 0;
                    }


                    if (exists)
                    {
                        skippedCount++;

                        continue;
                    }


                    // =============================================
                    // INSERT LEAVE CREDIT
                    // =============================================

                    using (
                        DbCommand insertCommand =
                            connection
                                .CreateCommand()
                    )
                    {
                        insertCommand.Transaction =
                            transaction;


                        insertCommand.CommandText = @"
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
VALUES
(
    @FromDate,

    @EmployeeCode,

    1,

    @ToDate,

    @LeaveCode,

    @FromDate,

    @ToDate,

    @LeaveCode,

    NULL,

    @SactOfficer,

    NULL,

    NULL,

    NULL,

    NULL,

    @LeaveAppId,

    @Operator,

    'A',

    @NumberOfDays
);
";


                        AddParameter(
                            insertCommand,
                            "@FromDate",
                            fromDate
                        );


                        AddParameter(
                            insertCommand,
                            "@ToDate",
                            toDate
                        );


                        AddParameter(
                            insertCommand,
                            "@EmployeeCode",
                            employeeCode
                        );


                        AddParameter(
                            insertCommand,
                            "@LeaveCode",
                            leaveCode
                        );


                        // =========================================
                        // LOGIN ID -> sact_officer
                        //
                        // Example:
                        // Login ID "0119"
                        // sact_officer = 119
                        // =========================================

                        AddParameter(
                            insertCommand,
                            "@SactOfficer",
                            loginUserCode
                        );


                        // =========================================
                        // LOGIN ID -> Operator
                        //
                        // Example:
                        // Login ID "0119"
                        // Operator = "0119"
                        // =========================================

                        AddParameter(
                            insertCommand,
                            "@Operator",
                            loginId
                        );


                        AddParameter(
                            insertCommand,
                            "@LeaveAppId",
                            leaveAppId
                        );


                        AddParameter(
                            insertCommand,
                            "@NumberOfDays",
                            numberOfDays
                        );


                        await insertCommand
                            .ExecuteNonQueryAsync();


                        insertedCount++;
                    }
                }


                // =================================================
                // COMMIT
                // =================================================

                await transaction
                    .CommitAsync();


                // =================================================
                // SUCCESS
                // =================================================

                return Json(new
                {
                    success = true,

                    message =
                        insertedCount +
                        " employee(s) leave " +
                        "credited successfully.",

                    leaveAppId =
                        leaveAppId,

                    loginId =
                        loginId,

                    sactOfficer =
                        loginUserCode,

                    operatorCode =
                        loginId,

                    leaveCode =
                        leaveCode,

                    leaveName =
                        leaveName,

                    leaveLimit =
                        leaveLimit,

                    numberOfDays =
                        numberOfDays,

                    insertedCount =
                        insertedCount,

                    skippedCount =
                        skippedCount
                });
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    try
                    {
                        await transaction
                            .RollbackAsync();
                    }
                    catch
                    {
                        // Ignore rollback exception
                    }
                }


                return Json(new
                {
                    success = false,

                    message =
                        "Unable to save " +
                        "Leave Credit. " +
                        ex.Message
                });
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction
                        .DisposeAsync();
                }


                if (
                    shouldClose &&
                    connection.State ==
                    ConnectionState.Open
                )
                {
                    await connection
                        .CloseAsync();
                }
            }
        }


        // =========================================================
        // CHECK LOGIN
        // =========================================================
        private bool IsLoggedIn()
        {
            return
                !string.IsNullOrWhiteSpace(
                    HttpContext.Session
                        .GetString(
                            "LoginId"
                        )
                );
        }


        // =========================================================
        // COMMON PARAMETER METHOD
        // =========================================================
        private static void AddParameter(
            DbCommand command,
            string parameterName,
            object? value)
        {
            DbParameter parameter =
                command.CreateParameter();

            parameter.ParameterName =
                parameterName;

            parameter.Value =
                value ?? DBNull.Value;

            command.Parameters.Add(
                parameter
            );
        }
    }
}