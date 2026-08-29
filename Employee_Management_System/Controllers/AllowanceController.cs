using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Employee_Management_System.Controllers
{
    public class AllowanceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AllowanceController(
            ApplicationDbContext context)
        {
            _context = context;
        }


        // =====================================================
        // ALLOWANCE LIST
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var allowances =
                await _context.AllowanceMasters
                    .AsNoTracking()
                    .OrderBy(x => x.Code)
                    .ToListAsync();

            return View(allowances);
        }


        // =====================================================
        // CREATE ALLOWANCE
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            AllowanceMaster model)
        {
            try
            {
                if (model.Code <= 0)
                {
                    TempData["Error"] =
                        "Allowance Code is required.";

                    return RedirectToAction(nameof(Index));
                }


                if (string.IsNullOrWhiteSpace(model.Name))
                {
                    TempData["Error"] =
                        "Allowance Name is required.";

                    return RedirectToAction(nameof(Index));
                }


                bool codeExists =
                    await _context.AllowanceMasters
                        .AnyAsync(x => x.Code == model.Code);

                if (codeExists)
                {
                    TempData["Error"] =
                        $"Allowance Code {model.Code} already exists.";

                    return RedirectToAction(nameof(Index));
                }


                model.Name =
                    model.Name?.Trim();

                model.ShortName =
                    model.ShortName?.Trim();

                model.Comp =
                    NormalizeYesNo(model.Comp);

                model.EffectOnPay =
                    NormalizeYesNo(model.EffectOnPay);

                model.EffectOnTrf =
                    NormalizeYesNo(model.EffectOnTrf);


                _context.AllowanceMasters.Add(model);

                await _context.SaveChangesAsync();


                TempData["Success"] =
                    "Allowance added successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "Unable to save allowance. " +
                    ex.Message;

                return RedirectToAction(nameof(Index));
            }
        }


        // =====================================================
        // GET ALLOWANCE FOR EDIT
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> GetAllowance(
            int code)
        {
            var allowance =
                await _context.AllowanceMasters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.Code == code
                    );

            if (allowance == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Allowance not found."
                });
            }


            return Json(new
            {
                success = true,

                data = new
                {
                    code = allowance.Code,
                    name = allowance.Name,
                    shortName = allowance.ShortName,
                    glc = allowance.GLC,
                    comp = allowance.Comp,
                    effectOnPay = allowance.EffectOnPay,
                    effectOnTrf = allowance.EffectOnTrf,
                    trfMinDays = allowance.TrfMinDays
                }
            });
        }


        // =====================================================
        // UPDATE ALLOWANCE
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(
            AllowanceMaster model)
        {
            try
            {
                var allowance =
                    await _context.AllowanceMasters
                        .FirstOrDefaultAsync(
                            x => x.Code == model.Code
                        );

                if (allowance == null)
                {
                    TempData["Error"] =
                        "Allowance not found.";

                    return RedirectToAction(nameof(Index));
                }


                if (string.IsNullOrWhiteSpace(model.Name))
                {
                    TempData["Error"] =
                        "Allowance Name is required.";

                    return RedirectToAction(nameof(Index));
                }


                allowance.Name =
                    model.Name?.Trim();

                allowance.ShortName =
                    model.ShortName?.Trim();

                allowance.GLC =
                    model.GLC;

                allowance.Comp =
                    NormalizeYesNo(model.Comp);

                allowance.EffectOnPay =
                    NormalizeYesNo(model.EffectOnPay);

                allowance.EffectOnTrf =
                    NormalizeYesNo(model.EffectOnTrf);

                allowance.TrfMinDays =
                    model.TrfMinDays;


                await _context.SaveChangesAsync();


                TempData["Success"] =
                    "Allowance updated successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "Unable to update allowance. " +
                    ex.Message;

                return RedirectToAction(nameof(Index));
            }
        }


        // =====================================================
        // DELETE ALLOWANCE
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(
            int code)
        {
            try
            {
                var allowance =
                    await _context.AllowanceMasters
                        .FirstOrDefaultAsync(
                            x => x.Code == code
                        );

                if (allowance == null)
                {
                    TempData["Error"] =
                        "Allowance not found.";

                    return RedirectToAction(nameof(Index));
                }


                _context.AllowanceMasters.Remove(
                    allowance
                );

                await _context.SaveChangesAsync();


                TempData["Success"] =
                    "Allowance deleted successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "Unable to delete allowance. " +
                    ex.Message;

                return RedirectToAction(nameof(Index));
            }
        }


        // =====================================================
        // Y / N NORMALIZER
        // =====================================================

        private static string? NormalizeYesNo(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }


            value =
                value.Trim().ToUpper();


            if (value == "Y")
            {
                return "Y";
            }


            if (value == "N")
            {
                return "N";
            }


            return null;
        }
    }
}