using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

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

        // POST: Employee/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Employee employee)
        {
            if (!ModelState.IsValid)
            {
                await LoadDropdown();
                return View(employee);
            }

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Employee saved successfully.";

            return RedirectToAction(nameof(Register));
        }

        [HttpGet]
        public async Task<IActionResult> SearchCaste(string term)
        {
            var result = await _context.Castes
                .Where(x => x.CastName.Contains(term))
                .Select(x => new
                {
                    id = x.Code,
                    text = x.CastName
                })
                .ToListAsync();

            return Json(result);
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
            if (string.IsNullOrEmpty(custId))
                return Json(null);

            var employee = await _context.Employees
                .Where(x => x.EmployeeCode == custId)
                .Select(x => new
                {
                    employeeCode = x.EmployeeCode,
                    employeeName = x.EmployeeName,
                    employeeType = x.EmployeeType,

                    gradeId = x.GradeId,
                    sectionId = x.SectionId,
                    branchId = x.BranchId,

                    joiningDate = x.JoiningDate,
                    permanentDate = x.PermanentDate,
                    lastSalaryDate = x.LastSalaryDate,
                    basicSalary = x.BasicSalary,

                    fatherName = x.FatherName,
                    address1 = x.Address1,
                    address2 = x.Address2,

                    religion = x.Religion,
                    caste = x.Caste,
                    gender = x.Gender,
                    birthDate = x.BirthDate
                })
                .FirstOrDefaultAsync();

            if (employee == null)
                return Json(null);

            return Json(employee);
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