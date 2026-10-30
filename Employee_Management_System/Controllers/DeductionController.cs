
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
            // Generate next Deduction Code for Add form
            int lastCode = await _context.DeductionMasters
                .AsNoTracking()
                .Select(x => (int?)x.Code)
                .MaxAsync() ?? 0;

            ViewBag.NextDeductionCode = checked(lastCode + 1);

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
            try
            {
                if (string.IsNullOrWhiteSpace(model.Name))
                {
                    TempData["Error"] = "Deduction Name is required.";
                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(model.Comp))
                {
                    TempData["Error"] = "Comp is required.";
                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(model.Flag))
                {
                    TempData["Error"] = "Flag is required.";
                    return RedirectToAction(nameof(Index));
                }

                // ==========================================
                // AUTOMATIC DEDUCTION CODE GENERATION
                // Always generate Code on server.
                // Do not trust Code submitted by browser.
                // ==========================================

                int lastCode = await _context.DeductionMasters
                    .AsNoTracking()
                    .Select(x => (int?)x.Code)
                    .MaxAsync() ?? 0;

                model.Code = checked(lastCode + 1);

                // ==========================================
                // NORMALIZE DATA
                // ==========================================

                model.Name = model.Name.Trim();
                model.ShortName = model.ShortName?.Trim();

                model.Comp = NormalizeYN(model.Comp);
                model.Flag = NormalizeYN(model.Flag);

                // ACTIVE FLAG: Default N
                model.Active = NormalizeYN(model.Active);

                if (string.IsNullOrWhiteSpace(model.Active))
                {
                    model.Active = "N";
                }

                // APPLY MONTH
                model.ApplyMonth = NormalizeApplyMonths(model.ApplyMonth);

                _context.DeductionMasters.Add(model);

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    $"Deduction added successfully. Code: {model.Code}";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                TempData["Error"] =
                    "Unable to save deduction. Please refresh the page and try again.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "Unable to save deduction. " + ex.Message;

                return RedirectToAction(nameof(Index));
            }
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
                    active = item.Active,
                    applyMonth = item.ApplyMonth
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
            try
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

                if (string.IsNullOrWhiteSpace(model.Comp))
                {
                    TempData["Error"] = "Comp is required.";
                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(model.Flag))
                {
                    TempData["Error"] = "Flag is required.";
                    return RedirectToAction(nameof(Index));
                }

                // ==========================================
                // UPDATE EXISTING RECORD
                // Code will NOT be changed.
                // ==========================================

                existing.Name = model.Name.Trim();
                existing.ShortName = model.ShortName?.Trim();
                existing.GLC = model.GLC;

                existing.Comp = NormalizeYN(model.Comp);
                existing.Flag = NormalizeYN(model.Flag);

                existing.HOCutting = model.HOCutting;
                existing.PrintInIncomeTaxReport =
                    model.PrintInIncomeTaxReport;

                // ACTIVE FLAG
                existing.Active = NormalizeYN(model.Active);

                if (string.IsNullOrWhiteSpace(existing.Active))
                {
                    existing.Active = "N";
                }

                // APPLY MONTH
                existing.ApplyMonth =
                    NormalizeApplyMonths(model.ApplyMonth);

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Deduction updated successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "Unable to update deduction. " + ex.Message;

                return RedirectToAction(nameof(Index));
            }
        }

        // ==========================================
        // DELETE
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int code)
        {
            try
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
            catch (Exception ex)
            {
                TempData["Error"] =
                    "Unable to delete deduction. " + ex.Message;

                return RedirectToAction(nameof(Index));
            }
        }

        // ==========================================
        // Y / N NORMALIZER
        // ==========================================

        private static string? NormalizeYN(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value.Trim().ToUpperInvariant();

            if (value == "Y" || value == "YES")
            {
                return "Y";
            }

            if (value == "N" || value == "NO")
            {
                return "N";
            }

            return null;
        }

        // ==========================================
        // APPLY MONTH NORMALIZER
        //
        // Input:    7,1,4,12
        // Database: 1,4,7,12
        // Only months 1 - 12 are allowed.
        // ==========================================

        private static string? NormalizeApplyMonths(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var months = value
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Select(x =>
                {
                    int.TryParse(x, out int month);
                    return month;
                })
                .Where(month => month >= 1 && month <= 12)
                .Distinct()
                .OrderBy(month => month)
                .ToList();

            if (months.Count == 0)
            {
                return null;
            }

            return string.Join(",", months);
        }
    }
}
