using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Employee_Management_System.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmployeeController(ApplicationDbContext context)
        {
            _context = context;
        }

        //======================== REGISTER (GET) ========================

        [HttpGet]
        public IActionResult Register()
        {
            LoadMasterData();

            return View(new Employee());
        }

        //======================== REGISTER (POST) ========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(Employee employee)
        {
            if (ModelState.IsValid)
            {
                _context.Employees.Add(employee);
                _context.SaveChanges();

                TempData["Success"] = "Employee Registered Successfully.";

                return RedirectToAction(nameof(Register));
            }

            LoadMasterData();

            return View(employee);
        }

        //======================== LOAD MASTER DATA ========================

        private void LoadMasterData()
        {
            ViewBag.Grades = new SelectList(
                _context.Grades.ToList(),
                "Id",
                "GradeName");

            ViewBag.Sections = new SelectList(
                _context.Sections.ToList(),
                "Id",
                "SectionName");

            ViewBag.Branches = new SelectList(
                _context.Branches.ToList(),
                "Id",
                "BranchName");
        }
    }
}