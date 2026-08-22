using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;

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
        // GET: Employee/Register
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Register()
        {
            await LoadDropdown();
            return View(new Employee());
        }

        // =========================================================
        // POST: Employee/Register
        // SAVE ONLY INTO PayMast
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Employee employee)
        {
            try
            {
                // =================================================
                // EMPLOYEE NAME VALIDATION
                // =================================================
                if (string.IsNullOrWhiteSpace(employee.EmployeeName))
                {
                    ModelState.AddModelError(
                        nameof(employee.EmployeeName),
                        "Employee Name is required."
                    );
                }

                if (!ModelState.IsValid)
                {
                    await LoadDropdown();
                    return View(employee);
                }

                // =================================================
                // READ FORM
                // =================================================
                var form = await Request.ReadFormAsync();

                // =================================================
                // CUSTOMER ID
                // =================================================
                string customerIdText = GetFirstNonEmpty(
                    form,
                    "CustomerId",
                    "CustomerID",
                    "customerId",
                    "custId",
                    "HiddenCustomerId"
                );

                decimal? customerId = null;

                if (decimal.TryParse(customerIdText, out decimal customerValue))
                {
                    customerId = customerValue;
                }

                if (!customerId.HasValue &&
                    !string.IsNullOrWhiteSpace(employee.CustomerId))
                {
                    if (decimal.TryParse(
                        employee.CustomerId,
                        out decimal modelCustomerValue))
                    {
                        customerId = modelCustomerValue;
                    }
                }

                if (!customerId.HasValue)
                {
                    ModelState.AddModelError(
                        "",
                        "Please search and select a valid Customer ID before saving."
                    );

                    await LoadDropdown();
                    return View(employee);
                }

                // =================================================
                // EMPLOYEE TYPE
                // =================================================
                string employeeTypeText = GetFirstNonEmpty(
                    form,
                    "EmployeeType",
                    "employeeType",
                    "Type",
                    "type",
                    "HiddenEmployeeType"
                );

                int? employeeType = null;

                if (int.TryParse(employeeTypeText, out int typeValue))
                {
                    employeeType = typeValue;
                }
                else
                {
                    switch ((employeeTypeText ?? "").Trim().ToLower())
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

                if (!employeeType.HasValue &&
                    !string.IsNullOrWhiteSpace(employee.EmployeeType))
                {
                    if (int.TryParse(
                        employee.EmployeeType,
                        out int modelTypeValue))
                    {
                        employeeType = modelTypeValue;
                    }
                    else
                    {
                        switch (employee.EmployeeType.Trim().ToLower())
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
                string gradeText = GetFirstNonEmpty(
                    form,
                    "GradeId",
                    "gradeId",
                    "Grade"
                );

                int? gradeCode = null;

                if (int.TryParse(gradeText, out int gradeValue))
                {
                    gradeCode = gradeValue;
                }

                if (!gradeCode.HasValue &&
                    employee.GradeId.HasValue)
                {
                    gradeCode = employee.GradeId.Value;
                }

                // =================================================
                // BASIC SALARY
                // =================================================
                string basicText = GetFirstNonEmpty(
                    form,
                    "BasicSalary",
                    "basicSalary",
                    "Basic",
                    "basic",
                    "HiddenBasicSalary"
                );

                double? basicSalary = null;

                if (double.TryParse(basicText, out double basicValue))
                {
                    basicSalary = basicValue;
                }

                if (!basicSalary.HasValue &&
                    employee.BasicSalary.HasValue)
                {
                    basicSalary =
                        Convert.ToDouble(employee.BasicSalary.Value);
                }

                // =================================================
                // RELIGION
                // =================================================
                decimal? religionCode = null;

                if (decimal.TryParse(
                    employee.Religion,
                    out decimal religionValue))
                {
                    religionCode = religionValue;
                }

                // =================================================
                // CASTE
                // =================================================
                int? casteCode = null;

                if (int.TryParse(
                    employee.Caste,
                    out int casteValue))
                {
                    casteCode = casteValue;
                }

                // =================================================
                // GENDER
                // PayMast.sex accepts M / F
                // =================================================
                string? genderCode = null;

                if (!string.IsNullOrWhiteSpace(employee.Gender))
                {
                    string gender =
                        employee.Gender.Trim();

                    if (gender.Equals(
                        "Male",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        genderCode = "M";
                    }
                    else if (gender.Equals(
                        "Female",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        genderCode = "F";
                    }
                    else if (gender.Equals(
                        "M",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        genderCode = "M";
                    }
                    else if (gender.Equals(
                        "F",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        genderCode = "F";
                    }
                }

                // =================================================
                // GENERATE NEW EMPLOYEE CODE
                // =================================================
                int lastEmployeeCode =
                    await _context.PayMasts
                        .AsNoTracking()
                        .Select(x => (int?)x.EmployeeCode)
                        .MaxAsync() ?? 0;

                int newEmployeeCode =
                    lastEmployeeCode + 1;

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
                // CREATE PAYMAST OBJECT
                // =================================================
                var payMast = new PayMast
                {
                    // GENERAL
                    EmployeeCode = newEmployeeCode,
                    CustomerId = customerId,
                    EmployeeName = employee.EmployeeName,
                    EmployeeType = employeeType,
                    JoiningDate = employee.JoiningDate,
                    PermanentDate = employee.PermanentDate,
                    GradeId = gradeCode,
                    BranchId = branchCode,
                    SectionId = employee.SectionId,
                    BasicSalary = basicSalary,
                    LastIncrementDate = employee.LastSalaryIncrementDate,
                    RetirementDate = employee.RetirementDate,

                    // OTHER
                    PensionFundOpeningBalance =
                        employee.PensionFundOpeningBalance.HasValue
                            ? Convert.ToDouble(
                                employee.PensionFundOpeningBalance.Value)
                            : null,

                    PFNo = employee.PFNumber,

                    PFOpeningBalance =
                        employee.PFOpeningBalance.HasValue
                            ? Convert.ToDouble(
                                employee.PFOpeningBalance.Value)
                            : null,

                    PANNo = employee.PANNumber,
                    ITSrNo = employee.ITSerialNumber,

                    PFSrNo =
                        int.TryParse(
                            employee.PFSerialNumber,
                            out int pfSerial)
                            ? pfSerial
                            : null,

                    PFBalance =
                        employee.PFBalance.HasValue
                            ? Convert.ToInt32(employee.PFBalance.Value)
                            : null,

                    AadharNo =
                        decimal.TryParse(
                            employee.AadhaarNumber,
                            out decimal aadhaar)
                            ? aadhaar
                            : null,

                    // ADDRESS
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

                    // PERSONAL
                    Religion = religionCode,
                    CasteId = casteCode,
                    Sex = genderCode,
                    BirthDate = employee.BirthDate,
                    BloodGroup = employee.BloodGroup,
                    IdentificationMark = employee.IdentificationMark,
                    Height = employee.Height,
                    LanguagesKnown = employee.KnownLanguage,
                    MotherTongue = employee.MotherTongue,
                    Qualification = employee.Education,
                    ModeOfSign = employee.ModeOfSign,

                    // ENTRY
                    EntryDate = DateTime.Now
                };

                // =================================================
                // SAVE ONLY INTO PayMast
                // =================================================
                _context.PayMasts.Add(payMast);

                await _context.SaveChangesAsync();

                // =================================================
                // SUCCESS POPUP DATA
                // =================================================
                TempData["Success"] =
                    "Employee created successfully!";

                TempData["SuccessCustomerName"] =
                    employee.EmployeeName ?? "";

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
        // HELPER METHOD
        // =========================================================
        private static string GetFirstNonEmpty(
            IFormCollection form,
            params string[] names)
        {
            foreach (var name in names)
            {
                if (form.ContainsKey(name))
                {
                    var value =
                        form[name].FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
            }

            return string.Empty;
        }

        // =========================================================
        // SEARCH RELIGION
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> SearchReligion(string term)
        {
            term ??= "";

            var result =
                await _context.Religions
                    .AsNoTracking()
                    .Where(x =>
                        x.ReligionName.Contains(term))
                    .Select(x => new
                    {
                        id = x.Code,
                        text = x.ReligionName
                    })
                    .ToListAsync();

            return Json(result);
        }

        // =========================================================
        // GET CUSTOMER
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

                custId = custId.Trim();

                var connection =
                    _context.Database.GetDbConnection();

                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }

                using var command =
                    connection.CreateCommand();

                command.CommandText = @"
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
                        p.Cast,

                        rm.name AS ReligionName,
                        cm.name AS CasteName

                    FROM dbo.prtymast p

                    LEFT JOIN dbo.ReligionMast rm
                        ON CONVERT(VARCHAR(50), rm.Code)
                        =
                        CONVERT(VARCHAR(50), p.Religion)

                    LEFT JOIN dbo.CastMast cm
                        ON CONVERT(VARCHAR(50), cm.Code)
                        =
                        CONVERT(VARCHAR(50), p.Cast)

                    WHERE
                        CONVERT(VARCHAR(50), p.CODE)
                        =
                        @custId
                ";

                var parameter =
                    command.CreateParameter();

                parameter.ParameterName =
                    "@custId";

                parameter.DbType =
                    DbType.String;

                parameter.Value =
                    custId;

                command.Parameters.Add(parameter);

                using var reader =
                    await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Customer ID " +
                            custId +
                            " was not found in prtymast."
                    });
                }

                string GetString(string column)
                {
                    int index =
                        reader.GetOrdinal(column);

                    if (reader.IsDBNull(index))
                    {
                        return "";
                    }

                    return reader
                        .GetValue(index)?
                        .ToString() ?? "";
                }

                string GetDate(string column)
                {
                    int index =
                        reader.GetOrdinal(column);

                    if (reader.IsDBNull(index))
                    {
                        return "";
                    }

                    object value =
                        reader.GetValue(index);

                    if (value is DateTime date)
                    {
                        return date.ToString("yyyy-MM-dd");
                    }

                    return value.ToString() ?? "";
                }

                return Json(new
                {
                    success = true,

                    data = new
                    {
                        customerId =
                            GetString("CODE"),

                        employeeCode =
                            GetString("CODE"),

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

                        casteCode =
                            GetString("Cast"),

                        caste =
                            GetString("CasteName")
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
                            "Database error: " +
                            ex.Message
                    });
            }
        }

        // =========================================================
        // GET RELIGION NAME
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetReligionName(decimal? code)
        {
            if (!code.HasValue)
            {
                return Json(null);
            }

            var religion =
                await _context.Religions
                    .AsNoTracking()
                    .Where(x =>
                        x.Code == code.Value)
                    .Select(x => new
                    {
                        id = x.Code,
                        name = x.ReligionName
                    })
                    .FirstOrDefaultAsync();

            return Json(religion);
        }

        // =========================================================
        // GET CASTE NAME
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetCasteName(decimal? code)
        {
            if (!code.HasValue)
            {
                return Json(null);
            }

            var caste =
                await _context.Castes
                    .AsNoTracking()
                    .Where(x =>
                        x.Code == code.Value)
                    .Select(x => new
                    {
                        id = x.Code,
                        name = x.CastName
                    })
                    .FirstOrDefaultAsync();

            return Json(caste);
        }

        // =========================================================
        // LOAD DROPDOWNS
        // =========================================================
        private async Task LoadDropdown()
        {
            var grades =
                await _context.Grades
                    .AsNoTracking()
                    .OrderBy(g =>
                        g.GradeName)
                    .ToListAsync();

            ViewBag.Grades =
                new SelectList(
                    grades,
                    "Code",
                    "GradeName"
                );

            var sections =
                await _context.Sections
                    .AsNoTracking()
                    .OrderBy(s =>
                        s.SectionName)
                    .ToListAsync();

            ViewBag.Sections =
                new SelectList(
                    sections,
                    "Code",
                    "SectionName"
                );

            var branches =
                await _context.Branches
                    .AsNoTracking()
                    .OrderBy(b =>
                        b.BranchName)
                    .ToListAsync();

            ViewBag.Branches =
                new SelectList(
                    branches,
                    "Code",
                    "BranchName"
                );

            var castes =
                await _context.Castes
                    .AsNoTracking()
                    .OrderBy(c =>
                        c.Code)
                    .ToListAsync();

            ViewBag.Castes =
                castes;

            var religions =
                await _context.Religions
                    .AsNoTracking()
                    .OrderBy(r =>
                        r.Code)
                    .ToListAsync();

            ViewBag.Religions =
                religions;
        }
    }
}