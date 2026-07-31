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


        public IActionResult Register()
        {
            ViewBag.Grades = _context.Grades.ToList();
            ViewBag.Sections = _context.Sections.ToList();
            ViewBag.Branches = _context.Branches.ToList();

            return View(new Employee());
        }
        [HttpPost]
        public IActionResult Register(Employee employee)
        {
            if (ModelState.IsValid)
            {
                _context.Employees.Add(employee);
                _context.SaveChanges();

                return RedirectToAction(nameof(Register));
            }

            ViewBag.Grades = _context.Grades.ToList();
            ViewBag.Sections = _context.Sections.ToList();
            ViewBag.Branches = _context.Branches.ToList();

            return View(employee);
        }
    }
}
