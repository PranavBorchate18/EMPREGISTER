using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Employee_Management_System.Controllers
{
    public class DeductionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DeductionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // LIST
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var deductions = await _context.DeductionMasters
                .AsNoTracking()
                .OrderBy(x => x.Code)
                .ToListAsync();

            return View(deductions);
        }


        // ==========================================
        // CREATE
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DeductionMaster model)
        {
            if (model.Code <= 0)
            {
                TempData["Error"] = "Deduction Code is required.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                TempData["Error"] = "Deduction Name is required.";
                return RedirectToAction(nameof(Index));
            }

            bool exists = await _context.DeductionMasters
                .AnyAsync(x => x.Code == model.Code);

            if (exists)
            {
                TempData["Error"] = "Deduction Code already exists.";
                return RedirectToAction(nameof(Index));
            }

            model.Name = model.Name?.Trim();
            model.ShortName = model.ShortName?.Trim();

            model.Comp = NormalizeYN(model.Comp);
            model.Flag = NormalizeYN(model.Flag);
            model.Active = NormalizeYN(model.Active);

            _context.DeductionMasters.Add(model);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Deduction added successfully.";

            return RedirectToAction(nameof(Index));
        }


        // ==========================================
        // GET SINGLE RECORD
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetDeduction(int code)
        {
            var item = await _context.DeductionMasters
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == code);

            if (item == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Deduction not found."
                });
            }

            return Json(new
            {
                success = true,
                data = new
                {
                    code = item.Code,
                    name = item.Name,
                    comp = item.Comp,
                    shortName = item.ShortName,
                    glc = item.GLC,
                    flag = item.Flag,
                    hoCutting = item.HOCutting,
                    printInIncomeTaxReport = item.PrintInIncomeTaxReport,
                    active = item.Active
                }
            });
        }


        // ==========================================
        // UPDATE
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(DeductionMaster model)
        {
            var existing = await _context.DeductionMasters
                .FirstOrDefaultAsync(x => x.Code == model.Code);

            if (existing == null)
            {
                TempData["Error"] = "Deduction not found.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                TempData["Error"] = "Deduction Name is required.";
                return RedirectToAction(nameof(Index));
            }

            existing.Name = model.Name?.Trim();
            existing.ShortName = model.ShortName?.Trim();
            existing.GLC = model.GLC;

            existing.Comp = NormalizeYN(model.Comp);
            existing.Flag = NormalizeYN(model.Flag);

            existing.HOCutting = model.HOCutting;
            existing.PrintInIncomeTaxReport =
                model.PrintInIncomeTaxReport;

            existing.Active = NormalizeYN(model.Active);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Deduction updated successfully.";

            return RedirectToAction(nameof(Index));
        }


        // ==========================================
        // DELETE
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int code)
        {
            var existing = await _context.DeductionMasters
                .FirstOrDefaultAsync(x => x.Code == code);

            if (existing == null)
            {
                TempData["Error"] = "Deduction not found.";
                return RedirectToAction(nameof(Index));
            }

            _context.DeductionMasters.Remove(existing);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Deduction deleted successfully.";

            return RedirectToAction(nameof(Index));
        }


        // ==========================================
        // Y / N NORMALIZE
        // ==========================================
        private static string? NormalizeYN(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            value = value.Trim().ToUpper();

            return value == "Y" ? "Y" :
                   value == "N" ? "N" :
                   null;
        }
    }
}