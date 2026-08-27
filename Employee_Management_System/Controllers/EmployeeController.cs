using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;
using System.Globalization;

namespace Employee_Management_System.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmployeeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // REGISTER - GET
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Register()
        {
            await LoadDropdown();
            return View(new Employee());
        }

        // =========================================================
        // REGISTER - POST
        // NEW EMPLOYEE SAVE TO PAYMAST
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Employee employee)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(employee.EmployeeName))
                {
                    ModelState.AddModelError(
                        nameof(employee.EmployeeName),
                        "Employee Name is required."
                    );
                }

                var form = await Request.ReadFormAsync();

                // =================================================
                // CUSTOMER ID
                // =================================================
                decimal? customerId = null;

                string? customerIdText = GetFirstNonEmpty(
                    form,
                    "CustomerId",
                    "CustomerID",
                    "customerId",
                    "custId",
                    "HiddenCustomerId"
                );

                if (!string.IsNullOrWhiteSpace(customerIdText) &&
                    decimal.TryParse(
                        customerIdText,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out decimal parsedCustomerId))
                {
                    customerId = parsedCustomerId;
                }
                else if (!string.IsNullOrWhiteSpace(employee.CustomerId) &&
                         decimal.TryParse(
                             employee.CustomerId,
                             NumberStyles.Any,
                             CultureInfo.InvariantCulture,
                             out decimal modelCustomerId))
                {
                    customerId = modelCustomerId;
                }

                if (!customerId.HasValue)
                {
                    ModelState.AddModelError(
                        "",
                        "Please search and select a valid Customer ID."
                    );
                }

                // =================================================
                // DUPLICATE CUSTOMER CHECK
                // =================================================
                if (customerId.HasValue)
                {
                    var duplicateEmployee =
                        await _context.PayMasts
                            .AsNoTracking()
                            .FirstOrDefaultAsync(
                                x => x.CustomerId == customerId.Value
                            );

                    if (duplicateEmployee != null)
                    {
                        TempData["DuplicateEmployee"] = "true";
                        TempData["DuplicateEmployeeName"] =
                            duplicateEmployee.EmployeeName ?? "";
                        TempData["DuplicateEmployeeCode"] =
                            duplicateEmployee.EmployeeCode.ToString();

                        await LoadDropdown();
                        return View(employee);
                    }
                }

                // =================================================
                // EMPLOYEE TYPE
                // =================================================
                int? employeeType = null;

                string? employeeTypeText = GetFirstNonEmpty(
                    form,
                    "EmployeeType",
                    "employeeType",
                    "Type",
                    "type",
                    "HiddenEmployeeType"
                );

                if (!string.IsNullOrWhiteSpace(employeeTypeText))
                {
                    if (int.TryParse(employeeTypeText, out int parsedType))
                    {
                        employeeType = parsedType;
                    }
                    else
                    {
                        switch (employeeTypeText.Trim().ToLower())
                        {
                            case "permanent":
                                employeeType = 1;
                                break;

                            case "temporary":
                                employeeType = 2;
                                break;

                            case "contract":
                                employeeType = 3;
                                break;
                        }
                    }
                }

                // =================================================
                // GRADE
                // =================================================
                int? gradeCode = employee.GradeId;

                string? gradeText = GetFirstNonEmpty(
                    form,
                    "GradeId",
                    "gradeId",
                    "Grade"
                );

                if (!string.IsNullOrWhiteSpace(gradeText) &&
                    int.TryParse(gradeText, out int parsedGrade))
                {
                    gradeCode = parsedGrade;
                }

                // =================================================
                // SECTION
                // =================================================
                int? sectionCode = employee.SectionId;

                // =================================================
                // BRANCH
                // =================================================
                int? branchCode = null;

                if (employee.BranchId.HasValue)
                {
                    branchCode =
                        Convert.ToInt32(employee.BranchId.Value);
                }

                // =================================================
                // BASIC SALARY
                // =================================================
                double? basicSalary = null;

                string? basicSalaryText = GetFirstNonEmpty(
                    form,
                    "BasicSalary",
                    "basicSalary",
                    "Basic",
                    "basic",
                    "HiddenBasicSalary"
                );

                if (!string.IsNullOrWhiteSpace(basicSalaryText))
                {
                    if (double.TryParse(
                        basicSalaryText,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out double parsedBasic))
                    {
                        basicSalary = parsedBasic;
                    }
                    else if (double.TryParse(
                        basicSalaryText,
                        out parsedBasic))
                    {
                        basicSalary = parsedBasic;
                    }
                }
                else if (employee.BasicSalary.HasValue)
                {
                    basicSalary =
                        Convert.ToDouble(employee.BasicSalary.Value);
                }

                // =================================================
                // RELIGION
                // =================================================
                decimal? religionCode = null;

                string? religionText = GetFirstNonEmpty(
                    form,
                    "Religion",
                    "ReligionId"
                );

                if (string.IsNullOrWhiteSpace(religionText))
                {
                    religionText = employee.Religion;
                }

                if (!string.IsNullOrWhiteSpace(religionText) &&
                    decimal.TryParse(
                        religionText,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out decimal parsedReligion))
                {
                    religionCode = parsedReligion;
                }

                // =================================================
                // CASTE
                // =================================================
                int? casteCode = null;

                string? casteText = GetFirstNonEmpty(
                    form,
                    "Caste",
                    "CasteId"
                );

                if (string.IsNullOrWhiteSpace(casteText))
                {
                    casteText = employee.Caste;
                }

                if (!string.IsNullOrWhiteSpace(casteText) &&
                    int.TryParse(
                        casteText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int parsedCaste))
                {
                    casteCode = parsedCaste;
                }

                // =================================================
                // GENDER
                // =================================================
                string? genderCode = null;

                if (!string.IsNullOrWhiteSpace(employee.Gender))
                {
                    string gender = employee.Gender.Trim();

                    if (gender.Equals(
                            "Male",
                            StringComparison.OrdinalIgnoreCase) ||
                        gender.Equals(
                            "M",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        genderCode = "M";
                    }
                    else if (gender.Equals(
                                 "Female",
                                 StringComparison.OrdinalIgnoreCase) ||
                             gender.Equals(
                                 "F",
                                 StringComparison.OrdinalIgnoreCase))
                    {
                        genderCode = "F";
                    }
                }

                // =================================================
                // MOTHER TONGUE
                // =================================================
                string? motherTongue =
                    string.IsNullOrWhiteSpace(employee.MotherTongue)
                        ? null
                        : employee.MotherTongue.Trim();

                if (string.Equals(
                    motherTongue,
                    "MOTHER TONGUE",
                    StringComparison.OrdinalIgnoreCase))
                {
                    motherTongue = null;
                }

                if (motherTongue != null &&
                    motherTongue.Length > 10)
                {
                    ModelState.AddModelError(
                        nameof(employee.MotherTongue),
                        "Mother Tongue can contain maximum 10 characters."
                    );
                }

                if (!ModelState.IsValid)
                {
                    await LoadDropdown();
                    return View(employee);
                }

                // =================================================
                // GENERATE EMPLOYEE CODE
                // MAX(PayMast.code) + 1
                // =================================================
                int lastEmployeeCode =
                    await _context.PayMasts
                        .AsNoTracking()
                        .Select(x => (int?)x.EmployeeCode)
                        .MaxAsync()
                    ?? 0;

                int newEmployeeCode =
                    lastEmployeeCode + 1;

                // =================================================
                // CREATE PAYMAST
                // =================================================
                var payMast = new PayMast
                {
                    EmployeeCode = newEmployeeCode,
                    CustomerId = customerId,

                    EmployeeName =
                        employee.EmployeeName?.Trim(),

                    EmployeeType = employeeType,

                    JoiningDate =
                        employee.JoiningDate,

                    PermanentDate =
                        employee.PermanentDate,

                    GradeId =
                        gradeCode,

                    BranchId =
                        branchCode,

                    SectionId =
                        sectionCode,

                    BasicSalary =
                        basicSalary,

                    LastIncrementDate =
                        employee.LastSalaryIncrementDate,

                    RetirementDate =
                        employee.RetirementDate,

                    PensionFundOpeningBalance =
                        employee.PensionFundOpeningBalance.HasValue
                            ? Convert.ToDouble(
                                employee.PensionFundOpeningBalance.Value)
                            : null,

                    PFNo =
                        employee.PFNumber,

                    PFOpeningBalance =
                        employee.PFOpeningBalance.HasValue
                            ? Convert.ToDouble(
                                employee.PFOpeningBalance.Value)
                            : null,

                    PANNo =
                        employee.PANNumber,

                    ITSrNo =
                        employee.ITSerialNumber,

                    PFSrNo =
                        int.TryParse(
                            employee.PFSerialNumber,
                            out int pfSerial)
                            ? pfSerial
                            : null,

                    PFBalance =
                        employee.PFBalance.HasValue
                            ? Convert.ToInt32(
                                employee.PFBalance.Value)
                            : null,

                    AadharNo =
                        decimal.TryParse(
                            employee.AadhaarNumber,
                            out decimal aadhaar)
                            ? aadhaar
                            : null,

                    CorrespondenceAddress1 =
                        employee.CorrespondenceAddress1,

                    CorrespondenceAddress2 =
                        employee.CorrespondenceAddress2,

                    PermanentAddress1 =
                        employee.PermanentAddress1,

                    PermanentAddress2 =
                        employee.PermanentAddress2,

                    FatherName =
                        employee.FatherName,

                    Religion =
                        religionCode,

                    CasteId =
                        casteCode,

                    Sex =
                        genderCode,

                    BirthDate =
                        employee.BirthDate,

                    BloodGroup =
                        employee.BloodGroup,

                    IdentificationMark =
                        employee.IdentificationMark,

                    Height =
                        employee.Height,

                    LanguagesKnown =
                        employee.KnownLanguage,

                    MotherTongue =
                        motherTongue,

                    Qualification =
                        employee.Education,

                    ModeOfSign =
                        employee.ModeOfSign,

                    EntryDate =
                        DateTime.Now
                };

                // =================================================
                // SAVE TO PAYMAST
                // =================================================
                _context.PayMasts.Add(payMast);
                await _context.SaveChangesAsync();

                // =================================================
                // SUCCESS POPUP
                // =================================================
                TempData["Success"] = "true";

                TempData["SuccessEmployeeName"] =
                    payMast.EmployeeName ?? "";

                TempData["SuccessEmployeeCode"] =
                    newEmployeeCode.ToString();

                return RedirectToAction(nameof(Register));
            }
            catch (DbUpdateException ex)
            {
                string errorMessage =
                    ex.InnerException?.Message ??
                    ex.Message;

                ModelState.AddModelError(
                    "",
                    "Database save error: " +
                    errorMessage
                );

                await LoadDropdown();

                return View(employee);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(
                    "",
                    "Unable to save employee: " +
                    ex.Message
                );

                await LoadDropdown();

                return View(employee);
            }
        }

        // =========================================================
        // CUSTOMER SEARCH FROM prtymast
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetEmployee(string custId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(custId))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Please enter Customer ID."
                    });
                }

                if (!decimal.TryParse(
                    custId.Trim(),
                    out decimal customerId))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Invalid Customer ID."
                    });
                }

                // Check duplicate
                var existingEmployee =
                    await _context.PayMasts
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            x => x.CustomerId == customerId
                        );

                if (existingEmployee != null)
                {
                    return Json(new
                    {
                        success = false,
                        alreadyExists = true,
                        message = "Employee already exists.",
                        employeeCode =
                            existingEmployee.EmployeeCode,
                        employeeName =
                            existingEmployee.EmployeeName
                    });
                }

                DbConnection connection =
                    _context.Database.GetDbConnection();

                bool shouldClose =
                    connection.State != ConnectionState.Open;

                if (shouldClose)
                {
                    await connection.OpenAsync();
                }

                try
                {
                    using DbCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                    @"
                        SELECT TOP 1
                            p.CODE,
                            p.name,
                            p.ADDR1,
                            p.ADDR2,
                            p.ADDR3,
                            p.ADDR4,
                            p.ADDR5,
                            p.PHONE,
                            p.PHONE1,
                            p.Mobile,
                            p.EMAIL_ID,
                            p.SEX,
                            p.birthdate,
                            p.FATHERNAME,
                            p.pan_no,
                            p.AdharNo,
                            p.NATIONALITY,
                            p.City,
                            p.State,
                            p.District,
                            p.Taluka,
                            p.Religion,
                            rm.Name AS ReligionName,
                            p.Cast,
                            cm.Name AS CasteName
                        FROM dbo.prtymast p
                        LEFT JOIN dbo.ReligionMast rm
                            ON rm.Code = p.Religion
                        LEFT JOIN dbo.CastMast cm
                            ON cm.Code = p.Cast
                        WHERE p.CODE = @custId
                    ";

                    DbParameter parameter =
                        command.CreateParameter();

                    parameter.ParameterName =
                        "@custId";

                    parameter.Value =
                        customerId;

                    command.Parameters.Add(parameter);

                    using DbDataReader reader =
                        await command.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        return Json(new
                        {
                            success = false,
                            message = "Customer not found."
                        });
                    }

                    string GetString(string columnName)
                    {
                        int ordinal =
                            reader.GetOrdinal(columnName);

                        if (reader.IsDBNull(ordinal))
                            return "";

                        return Convert.ToString(
                            reader.GetValue(ordinal)
                        ) ?? "";
                    }

                    string GetDate(string columnName)
                    {
                        int ordinal =
                            reader.GetOrdinal(columnName);

                        if (reader.IsDBNull(ordinal))
                            return "";

                        DateTime date =
                            Convert.ToDateTime(
                                reader.GetValue(ordinal)
                            );

                        return date.ToString("yyyy-MM-dd");
                    }

                    return Json(new
                    {
                        success = true,

                        data = new
                        {
                            customerId =
                                GetString("CODE"),

                            employeeCode = "",

                            employeeName =
                                GetString("name"),

                            address1 =
                                GetString("ADDR1"),

                            address2 =
                                GetString("ADDR2"),

                            address3 =
                                GetString("ADDR3"),

                            address4 =
                                GetString("ADDR4"),

                            address5 =
                                GetString("ADDR5"),

                            phone =
                                GetString("PHONE"),

                            phone1 =
                                GetString("PHONE1"),

                            mobile =
                                GetString("Mobile"),

                            email =
                                GetString("EMAIL_ID"),

                            gender =
                                GetString("SEX"),

                            birthDate =
                                GetDate("birthdate"),

                            fatherName =
                                GetString("FATHERNAME"),

                            panNumber =
                                GetString("pan_no"),

                            aadhaarNumber =
                                GetString("AdharNo"),

                            nationality =
                                GetString("NATIONALITY"),

                            city =
                                GetString("City"),

                            state =
                                GetString("State"),

                            district =
                                GetString("District"),

                            taluka =
                                GetString("Taluka"),

                            religionCode =
                                GetString("Religion"),

                            religion =
                                GetString("ReligionName"),

                            religionName =
                                GetString("ReligionName"),

                            casteCode =
                                GetString("Cast"),

                            caste =
                                GetString("CasteName"),

                            casteName =
                                GetString("CasteName")
                        }
                    });
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
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        success = false,
                        message =
                            "Unable to retrieve customer information: " +
                            ex.Message
                    }
                );
            }
        }
        // =========================================================
        // SEARCH EXISTING EMPLOYEES FROM PAYMAST
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> SearchExistingEmployees(
            string? searchBy,
            string? searchText
        )
        {
            try
            {
                searchBy =
                    (searchBy ?? "")
                        .Trim()
                        .ToLower();

                searchText =
                    (searchText ?? "")
                        .Trim();


                // =================================================
                // INITIAL LOAD - LATEST 20
                // =================================================
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    var latestEmployees =
                        await _context.PayMasts
                            .AsNoTracking()
                            .OrderByDescending(
                                x => x.EmployeeCode
                            )
                            .Select(
                                x => new
                                {
                                    employeeCode =
                                        x.EmployeeCode,

                                    employeeName =
                                        x.EmployeeName
                                }
                            )
                            .Take(20)
                            .ToListAsync();


                    return Json(new
                    {
                        success = true,
                        isInitialLoad = true,
                        data = latestEmployees
                    });
                }


                // =================================================
                // SEARCH BY EMPLOYEE CODE
                // =================================================
                if (searchBy == "code")
                {
                    if (!int.TryParse(
                        searchText,
                        out int employeeCode))
                    {
                        return Json(new
                        {
                            success = false,
                            message =
                                "Please enter a valid Employee Code."
                        });
                    }


                    var employees =
                        await _context.PayMasts
                            .AsNoTracking()
                            .Where(
                                x =>
                                    x.EmployeeCode ==
                                    employeeCode
                            )
                            .Select(
                                x => new
                                {
                                    employeeCode =
                                        x.EmployeeCode,

                                    employeeName =
                                        x.EmployeeName
                                }
                            )
                            .Take(50)
                            .ToListAsync();


                    return Json(new
                    {
                        success = true,
                        data = employees
                    });
                }


                // =================================================
                // SEARCH BY EMPLOYEE NAME
                // =================================================
                if (searchBy == "name")
                {
                    var employees =
                        await _context.PayMasts
                            .AsNoTracking()
                            .Where(
                                x =>
                                    x.EmployeeName != null &&
                                    x.EmployeeName.Contains(
                                        searchText
                                    )
                            )
                            .OrderBy(
                                x => x.EmployeeName
                            )
                            .ThenBy(
                                x => x.EmployeeCode
                            )
                            .Select(
                                x => new
                                {
                                    employeeCode =
                                        x.EmployeeCode,

                                    employeeName =
                                        x.EmployeeName
                                }
                            )
                            .Take(50)
                            .ToListAsync();


                    return Json(new
                    {
                        success = true,
                        data = employees
                    });
                }


                return Json(new
                {
                    success = false,
                    message =
                        "Please select Employee Code or Employee Name."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        success = false,
                        message =
                            "Unable to search employee: " +
                            ex.Message
                    }
                );
            }
        }


        // =========================================================
        // GET EXISTING EMPLOYEE FROM PAYMAST
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetExistingEmployee(
            int employeeCode
        )
        {
            try
            {
                var payMast =
                    await _context.PayMasts
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            x =>
                                x.EmployeeCode ==
                                employeeCode
                        );


                if (payMast == null)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Employee was not found in PayMast."
                    });
                }


                // =================================================
                // RELIGION NAME
                // =================================================
                string religionName = "";

                if (payMast.Religion.HasValue)
                {
                    religionName =
                        await _context.Religions
                            .AsNoTracking()
                            .Where(
                                x =>
                                    x.Code ==
                                    payMast.Religion.Value
                            )
                            .Select(
                                x => x.ReligionName
                            )
                            .FirstOrDefaultAsync()
                        ?? "";
                }


                // =================================================
                // CASTE NAME
                // =================================================
                string casteName = "";

                if (payMast.CasteId.HasValue)
                {
                    decimal casteCode =
                        Convert.ToDecimal(
                            payMast.CasteId.Value
                        );


                    casteName =
                        await _context.Castes
                            .AsNoTracking()
                            .Where(
                                x =>
                                    x.Code ==
                                    casteCode
                            )
                            .Select(
                                x => x.CastName
                            )
                            .FirstOrDefaultAsync()
                        ?? "";
                }


                return Json(new
                {
                    success = true,

                    data = new
                    {
                        employeeCode =
                            payMast.EmployeeCode,

                        customerId =
                            payMast.CustomerId,

                        employeeName =
                            payMast.EmployeeName,

                        employeeType =
                            payMast.EmployeeType,

                        joiningDate =
                            payMast.JoiningDate.HasValue
                                ? payMast.JoiningDate.Value
                                    .ToString("yyyy-MM-dd")
                                : "",

                        permanentDate =
                            payMast.PermanentDate.HasValue
                                ? payMast.PermanentDate.Value
                                    .ToString("yyyy-MM-dd")
                                : "",

                        gradeId =
                            payMast.GradeId,

                        branchId =
                            payMast.BranchId,

                        sectionId =
                            payMast.SectionId,

                        basicSalary =
                            payMast.BasicSalary,

                        lastSalaryIncrementDate =
                            payMast.LastIncrementDate.HasValue
                                ? payMast.LastIncrementDate.Value
                                    .ToString("yyyy-MM-dd")
                                : "",

                        retirementDate =
                            payMast.RetirementDate.HasValue
                                ? payMast.RetirementDate.Value
                                    .ToString("yyyy-MM-dd")
                                : "",

                        pensionFundOpeningBalance =
                            payMast.PensionFundOpeningBalance,

                        pfNumber =
                            payMast.PFNo,

                        pfOpeningBalance =
                            payMast.PFOpeningBalance,

                        panNumber =
                            payMast.PANNo,

                        itSerialNumber =
                            payMast.ITSrNo,

                        pfSerialNumber =
                            payMast.PFSrNo,

                        pfBalance =
                            payMast.PFBalance,

                        aadhaarNumber =
                            payMast.AadharNo,

                        correspondenceAddress1 =
                            payMast.CorrespondenceAddress1,

                        correspondenceAddress2 =
                            payMast.CorrespondenceAddress2,

                        permanentAddress1 =
                            payMast.PermanentAddress1,

                        permanentAddress2 =
                            payMast.PermanentAddress2,

                        fatherName =
                            payMast.FatherName,

                        religionCode =
                            payMast.Religion,

                        religion =
                            religionName,

                        religionName =
                            religionName,

                        casteCode =
                            payMast.CasteId,

                        caste =
                            casteName,

                        casteName =
                            casteName,

                        gender =
                            payMast.Sex,

                        birthDate =
                            payMast.BirthDate.HasValue
                                ? payMast.BirthDate.Value
                                    .ToString("yyyy-MM-dd")
                                : "",

                        bloodGroup =
                            payMast.BloodGroup,

                        identificationMark =
                            payMast.IdentificationMark,

                        height =
                            payMast.Height,

                        knownLanguage =
                            payMast.LanguagesKnown,

                        motherTongue =
                            payMast.MotherTongue,

                        education =
                            payMast.Qualification,

                        modeOfSign =
                            payMast.ModeOfSign
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        success = false,
                        message =
                            "Unable to load employee: " +
                            ex.Message
                    }
                );
            }
        }
        // =========================================================
        // UPDATE EXISTING EMPLOYEE
        // TARGET = PAYMAST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEmployee(
            Employee employee,
            int? existingEmployeeCode
        )
        {
            try
            {
                // =================================================
                // EMPLOYEE CODE
                // =================================================

                int employeeCode = 0;

                if (existingEmployeeCode.HasValue)
                {
                    employeeCode =
                        existingEmployeeCode.Value;
                }
                else if (
                    !string.IsNullOrWhiteSpace(
                        employee.EmployeeCode
                    )
                )
                {
                    int.TryParse(
                        employee.EmployeeCode,
                        out employeeCode
                    );
                }


                if (employeeCode <= 0)
                {
                    ModelState.AddModelError(
                        "",
                        "Please select an existing employee before updating."
                    );

                    await LoadDropdown();

                    return View(
                        "Register",
                        employee
                    );
                }


                // =================================================
                // FIND EXISTING EMPLOYEE
                // =================================================

                var existingEmployee =
                    await _context.PayMasts
                        .FirstOrDefaultAsync(
                            x =>
                                x.EmployeeCode ==
                                employeeCode
                        );


                if (existingEmployee == null)
                {
                    ModelState.AddModelError(
                        "",
                        "Employee was not found in PayMast."
                    );

                    await LoadDropdown();

                    return View(
                        "Register",
                        employee
                    );
                }


                if (
                    string.IsNullOrWhiteSpace(
                        employee.EmployeeName
                    )
                )
                {
                    ModelState.AddModelError(
                        nameof(employee.EmployeeName),
                        "Employee Name is required."
                    );

                    await LoadDropdown();

                    return View(
                        "Register",
                        employee
                    );
                }


                // =================================================
                // READ FORM
                // =================================================

                var updateForm =
                    await Request.ReadFormAsync();


                // =================================================
                // EMPLOYEE TYPE
                // =================================================

                int? employeeType =
                    existingEmployee.EmployeeType;

                string? employeeTypeText =
                    GetFirstNonEmpty(
                        updateForm,
                        "EmployeeType",
                        "employeeType",
                        "Type",
                        "type",
                        "HiddenEmployeeType"
                    );


                if (string.IsNullOrWhiteSpace(employeeTypeText))
                {
                    employeeTypeText =
                        employee.EmployeeType;
                }


                if (!string.IsNullOrWhiteSpace(employeeTypeText))
                {
                    if (
                        int.TryParse(
                            employeeTypeText,
                            out int typeValue
                        )
                    )
                    {
                        employeeType =
                            typeValue;
                    }
                    else
                    {
                        switch (
                            employeeTypeText
                                .Trim()
                                .ToLower()
                        )
                        {
                            case "permanent":

                                employeeType = 1;

                                break;


                            case "temporary":

                                employeeType = 2;

                                break;


                            case "contract":

                                employeeType = 3;

                                break;
                        }
                    }
                }


                // =================================================
                // CUSTOMER ID
                // =================================================

                decimal? customerId =
                    existingEmployee.CustomerId;

                string? customerText =
                    GetFirstNonEmpty(
                        updateForm,
                        "CustomerId",
                        "HiddenCustomerId"
                    );


                if (string.IsNullOrWhiteSpace(customerText))
                {
                    customerText =
                        employee.CustomerId;
                }


                if (
                    !string.IsNullOrWhiteSpace(customerText) &&
                    decimal.TryParse(
                        customerText,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out decimal customerValue
                    )
                )
                {
                    customerId =
                        customerValue;
                }


                // =================================================
                // RELIGION
                // =================================================

                decimal? religionCode =
                    existingEmployee.Religion;

                string? religionText =
                    GetFirstNonEmpty(
                        updateForm,
                        "Religion",
                        "ReligionId"
                    );


                if (string.IsNullOrWhiteSpace(religionText))
                {
                    religionText =
                        employee.Religion;
                }


                if (
                    !string.IsNullOrWhiteSpace(religionText) &&
                    decimal.TryParse(
                        religionText,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out decimal religionValue
                    )
                )
                {
                    religionCode =
                        religionValue;
                }


                // =================================================
                // CASTE
                // =================================================

                int? casteCode =
                    existingEmployee.CasteId;

                string? casteText =
                    GetFirstNonEmpty(
                        updateForm,
                        "Caste",
                        "CasteId"
                    );


                if (string.IsNullOrWhiteSpace(casteText))
                {
                    casteText =
                        employee.Caste;
                }


                if (
                    !string.IsNullOrWhiteSpace(casteText) &&
                    int.TryParse(
                        casteText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int casteValue
                    )
                )
                {
                    casteCode =
                        casteValue;
                }


                // =================================================
                // GENDER
                // =================================================

                string? genderCode =
                    existingEmployee.Sex;

                if (!string.IsNullOrWhiteSpace(employee.Gender))
                {
                    string gender =
                        employee.Gender.Trim();


                    if (
                        gender.Equals(
                            "Male",
                            StringComparison.OrdinalIgnoreCase
                        ) ||
                        gender.Equals(
                            "M",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        genderCode =
                            "M";
                    }
                    else if (
                        gender.Equals(
                            "Female",
                            StringComparison.OrdinalIgnoreCase
                        ) ||
                        gender.Equals(
                            "F",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        genderCode =
                            "F";
                    }
                }


                // =================================================
                // MOTHER TONGUE
                // =================================================

                string? motherTongue =
                    string.IsNullOrWhiteSpace(
                        employee.MotherTongue
                    )
                        ? null
                        : employee.MotherTongue.Trim();


                if (
                    string.Equals(
                        motherTongue,
                        "MOTHER TONGUE",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    motherTongue =
                        null;
                }


                if (
                    motherTongue != null &&
                    motherTongue.Length > 10
                )
                {
                    ModelState.AddModelError(
                        nameof(employee.MotherTongue),
                        "Mother Tongue can contain maximum 10 characters."
                    );


                    await LoadDropdown();


                    return View(
                        "Register",
                        employee
                    );
                }


                // =================================================
                // UPDATE PAYMAST RECORD
                // EMPLOYEE CODE DOES NOT CHANGE
                // =================================================

                existingEmployee.CustomerId =
                    customerId;

                existingEmployee.EmployeeName =
                    employee.EmployeeName.Trim();

                existingEmployee.EmployeeType =
                    employeeType;

                existingEmployee.JoiningDate =
                    employee.JoiningDate;

                existingEmployee.PermanentDate =
                    employee.PermanentDate;

                existingEmployee.GradeId =
                    employee.GradeId;

                existingEmployee.BranchId =
                    employee.BranchId.HasValue
                        ? Convert.ToInt32(
                            employee.BranchId.Value
                        )
                        : null;

                existingEmployee.SectionId =
                    employee.SectionId;

                existingEmployee.BasicSalary =
                    employee.BasicSalary.HasValue
                        ? Convert.ToDouble(
                            employee.BasicSalary.Value
                        )
                        : null;

                existingEmployee.LastIncrementDate =
                    employee.LastSalaryIncrementDate;

                existingEmployee.RetirementDate =
                    employee.RetirementDate;

                existingEmployee.PensionFundOpeningBalance =
                    employee.PensionFundOpeningBalance.HasValue
                        ? Convert.ToDouble(
                            employee.PensionFundOpeningBalance.Value
                        )
                        : null;

                existingEmployee.PFNo =
                    employee.PFNumber;

                existingEmployee.PFOpeningBalance =
                    employee.PFOpeningBalance.HasValue
                        ? Convert.ToDouble(
                            employee.PFOpeningBalance.Value
                        )
                        : null;

                existingEmployee.PANNo =
                    employee.PANNumber;

                existingEmployee.ITSrNo =
                    employee.ITSerialNumber;

                existingEmployee.PFSrNo =
                    int.TryParse(
                        employee.PFSerialNumber,
                        out int pfSerial
                    )
                        ? pfSerial
                        : null;

                existingEmployee.PFBalance =
                    employee.PFBalance.HasValue
                        ? Convert.ToInt32(
                            employee.PFBalance.Value
                        )
                        : null;

                existingEmployee.AadharNo =
                    decimal.TryParse(
                        employee.AadhaarNumber,
                        out decimal aadhaar
                    )
                        ? aadhaar
                        : null;


                // =================================================
                // ADDRESS INFORMATION
                // =================================================

                existingEmployee.CorrespondenceAddress1 =
                    employee.CorrespondenceAddress1;

                existingEmployee.CorrespondenceAddress2 =
                    employee.CorrespondenceAddress2;

                existingEmployee.PermanentAddress1 =
                    employee.PermanentAddress1;

                existingEmployee.PermanentAddress2 =
                    employee.PermanentAddress2;

                existingEmployee.FatherName =
                    employee.FatherName;


                // =================================================
                // RELIGION / CASTE
                // =================================================

                existingEmployee.Religion =
                    religionCode;

                existingEmployee.CasteId =
                    casteCode;


                // =================================================
                // PERSONAL INFORMATION
                // =================================================

                existingEmployee.Sex =
                    genderCode;

                existingEmployee.BirthDate =
                    employee.BirthDate;

                existingEmployee.BloodGroup =
                    employee.BloodGroup;

                existingEmployee.IdentificationMark =
                    employee.IdentificationMark;

                existingEmployee.Height =
                    employee.Height;

                existingEmployee.LanguagesKnown =
                    employee.KnownLanguage;

                existingEmployee.MotherTongue =
                    motherTongue;

                existingEmployee.Qualification =
                    employee.Education;

                existingEmployee.ModeOfSign =
                    employee.ModeOfSign;


                // =================================================
                // SAVE UPDATE TO PAYMAST
                // =================================================

                await _context.SaveChangesAsync();


                // =================================================
                // UPDATE SUCCESS POPUP
                // =================================================

                TempData["UpdateSuccess"] =
                    "true";

                TempData["UpdatedEmployeeName"] =
                    existingEmployee.EmployeeName
                    ?? "";

                TempData["UpdatedEmployeeCode"] =
                    existingEmployee.EmployeeCode
                        .ToString();


                return RedirectToAction(
                    nameof(Register)
                );
            }
            catch (DbUpdateException ex)
            {
                string errorMessage =
                    ex.InnerException?.Message
                    ??
                    ex.Message;


                ModelState.AddModelError(
                    "",
                    "Database update error: " +
                    errorMessage
                );


                await LoadDropdown();


                return View(
                    "Register",
                    employee
                );
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(
                    "",
                    "Unable to update employee: " +
                    ex.Message
                );


                await LoadDropdown();


                return View(
                    "Register",
                    employee
                );
            }
        }


        // =========================================================
        // GET RELIGION NAME
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> GetReligionName(
            decimal? code
        )
        {
            if (!code.HasValue)
            {
                return Json(new
                {
                    success =
                        false
                });
            }


            var religion =
                await _context.Religions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.Code ==
                            code.Value
                    );


            if (religion == null)
            {
                return Json(new
                {
                    success =
                        false
                });
            }


            return Json(new
            {
                success =
                    true,

                id =
                    religion.Code,

                name =
                    religion.ReligionName
            });
        }


        // =========================================================
        // GET CASTE NAME
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> GetCasteName(
            decimal? code
        )
        {
            if (!code.HasValue)
            {
                return Json(new
                {
                    success =
                        false
                });
            }


            var caste =
                await _context.Castes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.Code ==
                            code.Value
                    );


            if (caste == null)
            {
                return Json(new
                {
                    success =
                        false
                });
            }


            return Json(new
            {
                success =
                    true,

                id =
                    caste.Code,

                name =
                    caste.CastName
            });
        }


        // =========================================================
        // SEARCH RELIGION
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> SearchReligion(
            string? term
        )
        {
            var query =
                _context.Religions
                    .AsNoTracking();


            if (!string.IsNullOrWhiteSpace(term))
            {
                query =
                    query.Where(
                        x =>
                            x.ReligionName != null &&
                            x.ReligionName.Contains(
                                term
                            )
                    );
            }


            var result =
                await query
                    .OrderBy(
                        x =>
                            x.Code
                    )
                    .Select(
                        x =>
                            new
                            {
                                id =
                                    x.Code,

                                text =
                                    x.ReligionName
                            }
                    )
                    .Take(100)
                    .ToListAsync();


            return Json(result);
        }


        // =========================================================
        // LOAD DROPDOWNS
        // =========================================================

        private async Task LoadDropdown()
        {
            // Grade
            ViewBag.Grades =
                new SelectList(
                    await _context.Grades
                        .AsNoTracking()
                        .OrderBy(
                            x =>
                                x.GradeName
                        )
                        .ToListAsync(),

                    "Code",

                    "GradeName"
                );


            // Section
            ViewBag.Sections =
                new SelectList(
                    await _context.Sections
                        .AsNoTracking()
                        .OrderBy(
                            x =>
                                x.SectionName
                        )
                        .ToListAsync(),

                    "Code",

                    "SectionName"
                );


            // Branch
            ViewBag.Branches =
                new SelectList(
                    await _context.Branches
                        .AsNoTracking()
                        .OrderBy(
                            x =>
                                x.BranchName
                        )
                        .ToListAsync(),

                    "Code",

                    "BranchName"
                );


            // Caste
            ViewBag.Castes =
                await _context.Castes
                    .AsNoTracking()
                    .OrderBy(
                        x =>
                            x.Code
                    )
                    .ToListAsync();


            // Religion
            ViewBag.Religions =
                await _context.Religions
                    .AsNoTracking()
                    .OrderBy(
                        x =>
                            x.Code
                    )
                    .ToListAsync();
        }


        // =========================================================
        // GET FIRST NON EMPTY FORM VALUE
        // =========================================================

        private static string? GetFirstNonEmpty(
            IFormCollection form,
            params string[] names
        )
        {
            foreach (string name in names)
            {
                if (
                    form.TryGetValue(
                        name,
                        out var value
                    ) &&
                    !string.IsNullOrWhiteSpace(
                        value.ToString()
                    )
                )
                {
                    return value
                        .ToString()
                        .Trim();
                }
            }


            return null;
        }
    }
}