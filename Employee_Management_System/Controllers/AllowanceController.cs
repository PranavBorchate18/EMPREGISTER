
using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Employee_Management_System.Controllers
{
    public class AllowanceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AllowanceController(ApplicationDbContext context)
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

            // Generate next allowance code for Add form
            int lastCode = await _context.AllowanceMasters
                .AsNoTracking()
                .Select(x => (int?)x.Code)
                .MaxAsync() ?? 0;

            ViewBag.NextAllowanceCode = lastCode + 1;

            return View(allowances);
        }

        // =====================================================
        // CREATE ALLOWANCE
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AllowanceMaster model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.Name))
                {
                    TempData["Error"] = "Allowance Name is required.";
                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(model.Comp))
                {
                    TempData["Error"] = "Comp is required.";
                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(model.EffectOnPay))
                {
                    TempData["Error"] = "Effect On Pay is required.";
                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(model.EffectOnTrf))
                {
                    TempData["Error"] = "Effect On Trf is required.";
                    return RedirectToAction(nameof(Index));
                }

                model.Name = model.Name.Trim();
                model.ShortName = model.ShortName?.Trim();

                model.Comp = NormalizeYesNo(model.Comp);
                model.EffectOnPay = NormalizeYesNo(model.EffectOnPay);
                model.EffectOnTrf = NormalizeYesNo(model.EffectOnTrf);

                // APPLY MONTH
                model.ApplyMonth =
                    NormalizeApplyMonths(model.ApplyMonth);

                // ACTIVE
                model.Active = NormalizeActive(model.Active);

                if (string.IsNullOrWhiteSpace(model.Active))
                {
                    model.Active = "No";
                }

                // ALLOWANCE TYPE
                // 1 = General, 2 = HRA, 3 = DA
                model.AllowType =
                    NormalizeAllowType(model.AllowType);

                // =================================================
                // AUTOMATIC ALLOWANCE CODE GENERATION
                // Browser submitted Code is ignored.
                // Serializable transaction helps prevent duplicate
                // codes when multiple users save simultaneously.
                // =================================================

                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                int lastCode = await _context.AllowanceMasters
                    .AsNoTracking()
                    .Select(x => (int?)x.Code)
                    .MaxAsync() ?? 0;

                model.Code = checked(lastCode + 1);

                _context.AllowanceMasters.Add(model);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["Success"] =
                    "Allowance added successfully. Allowance Code: "
                    + model.Code;

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _loggerLogError(ex);

                TempData["Error"] =
                    "Unable to save allowance. " + ex.Message;

                return RedirectToAction(nameof(Index));
            }
        }

        // =====================================================
        // GET ALLOWANCE FOR EDIT
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> GetAllowance(int code)
        {
            var allowance =
                await _context.AllowanceMasters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Code == code);

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
                    trfMinDays = allowance.TrfMinDays,
                    applyMonth = allowance.ApplyMonth,

                    allowType = NormalizeAllowType(
                        allowance.AllowType),

                    active = allowance.Active
                }
            });
        }

        // =====================================================
        // UPDATE ALLOWANCE
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(AllowanceMaster model)
        {
            try
            {
                var allowance =
                    await _context.AllowanceMasters
                        .FirstOrDefaultAsync(x => x.Code == model.Code);

                if (allowance == null)
                {
                    TempData["Error"] = "Allowance not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(model.Name))
                {
                    TempData["Error"] = "Allowance Name is required.";
                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(model.Comp))
                {
                    TempData["Error"] = "Comp is required.";
                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(model.EffectOnPay))
                {
                    TempData["Error"] = "Effect On Pay is required.";
                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(model.EffectOnTrf))
                {
                    TempData["Error"] = "Effect On Trf is required.";
                    return RedirectToAction(nameof(Index));
                }

                allowance.Name = model.Name.Trim();
                allowance.ShortName = model.ShortName?.Trim();
                allowance.GLC = model.GLC;

                allowance.Comp =
                    NormalizeYesNo(model.Comp);

                allowance.EffectOnPay =
                    NormalizeYesNo(model.EffectOnPay);

                allowance.EffectOnTrf =
                    NormalizeYesNo(model.EffectOnTrf);

                allowance.TrfMinDays = model.TrfMinDays;

                allowance.ApplyMonth =
                    NormalizeApplyMonths(model.ApplyMonth);

                allowance.Active =
                    NormalizeActive(model.Active);

                if (string.IsNullOrWhiteSpace(allowance.Active))
                {
                    allowance.Active = "No";
                }

                allowance.AllowType =
                    NormalizeAllowType(model.AllowType);

                // Code is intentionally not updated.
                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Allowance updated successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _loggerLogError(ex);

                TempData["Error"] =
                    "Unable to update allowance. " + ex.Message;

                return RedirectToAction(nameof(Index));
            }
        }

        // =====================================================
        // DELETE ALLOWANCE
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int code)
        {
            try
            {
                var allowance =
                    await _context.AllowanceMasters
                        .FirstOrDefaultAsync(x => x.Code == code);

                if (allowance == null)
                {
                    TempData["Error"] = "Allowance not found.";
                    return RedirectToAction(nameof(Index));
                }

                _context.AllowanceMasters.Remove(allowance);

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Allowance deleted successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _loggerLogError(ex);

                TempData["Error"] =
                    "Unable to delete allowance. " + ex.Message;

                return RedirectToAction(nameof(Index));
            }
        }

        // =====================================================
        // ALLOWANCE TYPE NORMALIZER
        // 1 = General, 2 = HRA, 3 = DA
        // =====================================================

        private static int NormalizeAllowType(int? value)
        {
            if (
                value == AllowanceMaster.TypeHra ||
                value == AllowanceMaster.TypeDa
            )
            {
                return value.Value;
            }

            return AllowanceMaster.TypeGeneral;
        }

        // =====================================================
        // Y / N NORMALIZER
        // =====================================================

        private static string? NormalizeYesNo(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value.Trim().ToUpper();

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

        // =====================================================
        // ACTIVE NORMALIZER
        // =====================================================

        private static string? NormalizeActive(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value.Trim();

            if (
                value.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("Y", StringComparison.OrdinalIgnoreCase)
            )
            {
                return "Yes";
            }

            if (
                value.Equals("No", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("N", StringComparison.OrdinalIgnoreCase)
            )
            {
                return "No";
            }

            return null;
        }

        // =====================================================
        // APPLY MONTH NORMALIZER
        // =====================================================

        private static string? NormalizeApplyMonths(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var months = value
                .Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Select(x =>
                {
                    int.TryParse(x, out int month);
                    return month;
                })
                .Where(x => x >= 1 && x <= 12)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            if (months.Count == 0)
            {
                return null;
            }

            return string.Join(",", months);
        }

        // =====================================================
        // ERROR LOGGING
        // =====================================================

        private void _loggerLogError(Exception ex)
        {
            // Error details are stored in server output.
            Console.WriteLine(ex);
        }
    }
}
