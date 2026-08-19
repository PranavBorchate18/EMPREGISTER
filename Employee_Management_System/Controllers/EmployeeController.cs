using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis.Scripting;
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

        // GET: Employee/Register
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
                // ==============================================
                // EMPLOYEE NAME VALIDATION
                // ==============================================

                if (string.IsNullOrWhiteSpace(employee.EmployeeName))
                {
                    ModelState.AddModelError(
                        "EmployeeName",
                        "Employee Name is required.");
                }

                if (!ModelState.IsValid)
                {
                    await LoadDropdown();
                    return View(employee);
                }


                // ==============================================
                // READ FORM
                // ==============================================

                var form = await Request.ReadFormAsync();


                // ==============================================
                // CUSTOMER ID
                // Customer ID → Party_code
                // ==============================================

                string customerIdText = GetFirstNonEmpty(
                    form,
                    "CustomerId",
                    "CustomerID",
                    "customerId",
                    "custId",
                    "HiddenCustomerId"
                );

                decimal? customerId = null;

                if (decimal.TryParse(
                    customerIdText,
                    out decimal customerValue))
                {
                    customerId = customerValue;
                }

                // Model fallback
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


                // ==============================================
                // EMPLOYEE TYPE
                // EmployeeType → type
                // ==============================================

                string employeeTypeText = GetFirstNonEmpty(
                    form,
                    "EmployeeType",
                    "employeeType",
                    "Type",
                    "type",
                    "HiddenEmployeeType"
                );

                int? employeeType = null;

                if (int.TryParse(
                    employeeTypeText,
                    out int typeValue))
                {
                    employeeType = typeValue;
                }

                // Model fallback
                if (!employeeType.HasValue &&
                    !string.IsNullOrWhiteSpace(employee.EmployeeType))
                {
                    if (int.TryParse(
                        employee.EmployeeType,
                        out int modelTypeValue))
                    {
                        employeeType = modelTypeValue;
                    }
                }


                // ==============================================
                // GRADE
                // GradeId → grad_Code
                // ==============================================

                string gradeText = GetFirstNonEmpty(
                    form,
                    "GradeId",
                    "gradeId",
                    "Grade"
                );

                int? gradeCode = null;

                if (int.TryParse(
                    gradeText,
                    out int gradeValue))
                {
                    gradeCode = gradeValue;
                }

                if (!gradeCode.HasValue &&
                    employee.GradeId.HasValue)
                {
                    gradeCode = employee.GradeId.Value;
                }


                // ==============================================
                // BASIC SALARY
                // BasicSalary → basic
                // ==============================================

                string basicText = GetFirstNonEmpty(
                    form,
                    "BasicSalary",
                    "basicSalary",
                    "Basic",
                    "basic",
                    "HiddenBasicSalary"
                );

                double? basicSalary = null;

                if (double.TryParse(
                    basicText,
                    out double basicValue))
                {
                    basicSalary = basicValue;
                }

                if (!basicSalary.HasValue &&
                    employee.BasicSalary.HasValue)
                {
                    basicSalary =
                        Convert.ToDouble(employee.BasicSalary.Value);
                }


                // ==============================================
                // RELIGION
                // ==============================================

                decimal? religionCode = null;

                if (decimal.TryParse(
                    employee.Religion,
                    out decimal religionValue))
                {
                    religionCode = religionValue;
                }


                // ==============================================
                // CASTE
                // ==============================================

                int? casteCode = null;

                if (int.TryParse(
                    employee.Caste,
                    out int casteValue))
                {
                    casteCode = casteValue;
                }


                // ==============================================
                // EMPLOYEE CODE
                // Get next code from PayMast
                // ==============================================

                int lastEmployeeCode =
                    await _context.PayMasts
                        .Select(x => (int?)x.EmployeeCode)
                        .MaxAsync() ?? 0;

                int newEmployeeCode =
                    lastEmployeeCode + 1;


                // ==============================================
                // BRANCH
                // ==============================================

                int? branchCode = null;

                if (employee.BranchId.HasValue)
                {
                    branchCode =
                        Convert.ToInt32(employee.BranchId.Value);
                }


                // ==============================================
                // CREATE PAYMAST
                // ==============================================

                var payMast = new PayMast
                {
                    // ------------------------------------------
                    // GENERAL
                    // ------------------------------------------

                    EmployeeCode = newEmployeeCode,

                    // Customer ID → Party_code
                    CustomerId = customerId,

                    // Employee Name → name
                    EmployeeName = employee.EmployeeName,

                    // Employee Type → type
                    EmployeeType = employeeType,

                    // Joining Date → join_date
                    JoiningDate = employee.JoiningDate,

                    // Permanent Date
                    PermanentDate = employee.PermanentDate,

                    // Grade → grad_Code
                    GradeId = gradeCode,

                    // Branch → brnc_code
                    BranchId = branchCode,

                    // Section → section
                    SectionId = employee.SectionId,

                    // Basic Salary → basic
                    BasicSalary = basicSalary,

                    // Last Increment
                    LastIncrementDate =
                        employee.LastSalaryIncrementDate,

                    // Retirement
                    RetirementDate =
                        employee.RetirementDate,


                    // ------------------------------------------
                    // OTHER
                    // ------------------------------------------

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


                    // ------------------------------------------
                    // ADDRESS
                    // ------------------------------------------

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


                    // ------------------------------------------
                    // PERSONAL
                    // ------------------------------------------

                    Religion =
                        religionCode,

                    CasteId =
                        casteCode,

                    Sex =
                        employee.Gender,

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
                        employee.MotherTongue,

                    Qualification =
                        employee.Education,

                    ModeOfSign =
                        employee.ModeOfSign,


                    // ------------------------------------------
                    // ENTRY
                    // ------------------------------------------

                    EntryDate = DateTime.Now
                };


                // ==============================================
                // IMPORTANT
                // DO NOT SAVE TO Employee TABLE
                // ==============================================

                _context.PayMasts.Add(payMast);

                await _context.SaveChangesAsync();


                // ==============================================
                // SUCCESS
                // ==============================================

                TempData["Success"] =
                    "Employee saved successfully. " +
                    "Employee Code: " +
                    newEmployeeCode;

                return RedirectToAction(nameof(Register));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(
                    "",
                    "Unable to save employee: " + ex.Message);

                await LoadDropdown();

                return View(employee);
            }
        }


        // =========================================================
        // HELPER METHOD
        // Gets first NON-EMPTY form value
        // =========================================================

        private static string GetFirstNonEmpty(
            IFormCollection form,
            params string[] names)
        {
            foreach (var name in names)
            {
                if (form.ContainsKey(name))
                {
                    var value = form[name]
                        .FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
            }

            return string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> SearchReligion(string term)
        {
            var result = await _context.Religions
                .Where(x => x.ReligionName.Contains(term))
                .Select(x => new
                {
                    id = x.Code,
                    text = x.ReligionName
                })
                .ToListAsync();

            return Json(result);
        }

        //[HttpGet]
        //public async Task<IActionResult> GetEmployee(string custId)
        //{
        //    var employee = await _context.Employees
        //        .FirstOrDefaultAsync(x => x.CustId == custId);

        //    if (employee == null)
        //        return Json(null);

        //    return Json(employee);
        //}

        [HttpGet]
        public async Task<IActionResult> GetEmployee(string custId)
        {
            try
            {
                // ==========================================
                // CHECK CUSTOMER ID
                // ==========================================

                if (string.IsNullOrWhiteSpace(custId))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Please enter Customer ID."
                    });
                }

                custId = custId.Trim();


                // ==========================================
                // DATABASE CONNECTION
                // ==========================================

                var connection = _context.Database.GetDbConnection();

                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }


                // ==========================================
                // CUSTOMER QUERY
                // ==========================================

                using var command = connection.CreateCommand();

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
                 = CONVERT(VARCHAR(50), p.Religion)

            LEFT JOIN dbo.CastMast cm
                ON CONVERT(VARCHAR(50), cm.Code)
                 = CONVERT(VARCHAR(50), p.Cast)

            WHERE CONVERT(VARCHAR(50), p.CODE) = @custId
        ";


                // ==========================================
                // PARAMETER
                // ==========================================

                var parameter = command.CreateParameter();

                parameter.ParameterName = "@custId";
                parameter.DbType = DbType.String;
                parameter.Value = custId;

                command.Parameters.Add(parameter);


                // ==========================================
                // EXECUTE
                // ==========================================

                using var reader = await command.ExecuteReaderAsync();


                // ==========================================
                // CUSTOMER NOT FOUND
                // ==========================================

                if (!await reader.ReadAsync())
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Customer ID " + custId +
                            " was not found in prtymast."
                    });
                }


                // ==========================================
                // STRING HELPER
                // ==========================================

                string GetString(string column)
                {
                    int index = reader.GetOrdinal(column);

                    if (reader.IsDBNull(index))
                    {
                        return "";
                    }

                    return reader.GetValue(index)?.ToString() ?? "";
                }


                // ==========================================
                // DATE HELPER
                // ==========================================

                string GetDate(string column)
                {
                    int index = reader.GetOrdinal(column);

                    if (reader.IsDBNull(index))
                    {
                        return "";
                    }

                    object value = reader.GetValue(index);

                    if (value is DateTime date)
                    {
                        return date.ToString("yyyy-MM-dd");
                    }

                    return value.ToString() ?? "";
                }


                // ==========================================
                // RETURN CUSTOMER INFORMATION
                // ==========================================

                return Json(new
                {
                    success = true,

                    data = new
                    {
                        // -------------------------------
                        // Customer
                        // -------------------------------

                        customerId = GetString("CODE"),

                        employeeCode = GetString("CODE"),

                        employeeName = GetString("name"),


                        // -------------------------------
                        // Address
                        // -------------------------------

                        address1 = GetString("ADDR1"),
                        address2 = GetString("ADDR2"),
                        address3 = GetString("ADDR3"),
                        address4 = GetString("ADDR4"),
                        address5 = GetString("ADDR5"),


                        // -------------------------------
                        // Contact
                        // -------------------------------

                        phone = GetString("PHONE"),
                        phone1 = GetString("PHONE1"),
                        mobile = GetString("Mobile"),

                        email = GetString("EMAIL_ID"),


                        // -------------------------------
                        // Personal
                        // -------------------------------

                        gender = GetString("SEX"),

                        birthDate = GetDate("birthdate"),

                        fatherName = GetString("FATHERNAME"),


                        // -------------------------------
                        // Documents
                        // -------------------------------

                        panNumber = GetString("pan_no"),

                        aadhaarNumber = GetString("AdharNo"),


                        // -------------------------------
                        // Location
                        // -------------------------------

                        nationality = GetString("NATIONALITY"),

                        city = GetString("City"),

                        state = GetString("State"),

                        district = GetString("District"),

                        taluka = GetString("Taluka"),


                        // -------------------------------
                        // RELIGION
                        // -------------------------------

                        religionCode = GetString("Religion"),

                        religion = GetString("ReligionName"),


                        // -------------------------------
                        // CASTE
                        // -------------------------------

                        casteCode = GetString("Cast"),

                        caste = GetString("CasteName")
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Database error: " + ex.Message
                });
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetReligionName(decimal? code)
        {
            if (!code.HasValue)
                return Json(null);

            var religion = await _context.Religions
                .AsNoTracking()
                .Where(x => x.Code == code.Value)
                .Select(x => new
                {
                    id = x.Code,
                    name = x.ReligionName
                })
                .FirstOrDefaultAsync();

            return Json(religion);
        }

        [HttpGet]
        public async Task<IActionResult> GetCasteName(decimal? code)
        {
            if (!code.HasValue)
                return Json(null);

            var caste = await _context.Castes
                .AsNoTracking()
                .Where(x => x.Code == code.Value)
                .Select(x => new
                {
                    id = x.Code,
                    name = x.CastName
                })
                .FirstOrDefaultAsync();

            return Json(caste);
        }

        private async Task LoadDropdown()
        {
            // Grade
            var grades = await _context.Grades
                .AsNoTracking()
                .OrderBy(g => g.GradeName)
                .ToListAsync();

            ViewBag.Grades = new SelectList(grades, "Code", "GradeName");

            // Section
            var sections = await _context.Sections
                .AsNoTracking()
                .OrderBy(s => s.SectionName)
                .ToListAsync();

            ViewBag.Sections = new SelectList(sections, "Code", "SectionName");

            // Branch
            var branches = await _context.Branches
                .AsNoTracking()
                .OrderBy(b => b.BranchName)
                .ToListAsync();

            ViewBag.Branches = new SelectList(branches, "Code", "BranchName");

            //===============================
            // Caste Master (Order By Code)
            //===============================

            var castes = await _context.Castes
                .AsNoTracking()
                .OrderBy(c => c.Code)
                .ToListAsync();

            ViewBag.Castes = castes;


            //===============================
            // Religion Master (Order By Code)
            //===============================

            var religions = await _context.Religions
                .AsNoTracking()
                .OrderBy(r => r.Code)
                .ToListAsync();

            ViewBag.Religions = religions;
        }
    }
}