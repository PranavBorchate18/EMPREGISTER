using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Employee_Management_System.Controllers
{
    public class GradeWiseAllowDeductionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GradeWiseAllowDeductionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // GRADE WISE ALLOWANCE / DEDUCTION
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Index(
            int? allowanceCode,
            int? deductionCode)
        {
            var model = new GradeWiseAllowDeductionViewModel();


            // ========================================================
            // ALLOWANCE LIST
            // dbo.allwmast
            // ========================================================

            model.Allowances = await _context.AllowanceMasters
                .AsNoTracking()
                .OrderBy(x => x.Code)
                .ToListAsync();


            // ========================================================
            // DEDUCTION LIST
            // dbo.dedumast
            // ========================================================

            model.Deductions = await _context.DeductionMasters
                .AsNoTracking()
                .OrderBy(x => x.Code)
                .ToListAsync();


            // ========================================================
            // SELECTED ALLOWANCE DETAILS
            //
            // allwmast.code = allwfoot.allw_code
            // ========================================================

            if (allowanceCode.HasValue)
            {
                var selectedAllowance =
                    await _context.AllowanceMasters
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            x => x.Code == allowanceCode.Value);

                if (selectedAllowance != null)
                {
                    // Selected Allowance Code
                    model.SelectedAllowanceCode =
                        selectedAllowance.Code;

                    // Selected Allowance Name
                    model.SelectedAllowanceName =
                        selectedAllowance.Name;


                    // allwfoot.allw_code is decimal
                    decimal selectedCode =
                        selectedAllowance.Code;


                    // Get Grade Wise Allowance Details
                    model.AllowanceDetails =
                        await _context.AllwFoots
                            .AsNoTracking()
                            .Where(
                                x => x.AllowCode == selectedCode)
                            .OrderBy(
                                x => x.GradeCode)
                            .ToListAsync();
                }
            }


            // ========================================================
            // SELECTED DEDUCTION DETAILS
            //
            // dedumast.code = dedufoot.dedu_code
            // ========================================================

            if (deductionCode.HasValue)
            {
                var selectedDeduction =
                    await _context.DeductionMasters
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            x => x.Code == deductionCode.Value);

                if (selectedDeduction != null)
                {
                    // Selected Deduction Code
                    model.SelectedDeductionCode =
                        selectedDeduction.Code;

                    // Selected Deduction Name
                    model.SelectedDeductionName =
                        selectedDeduction.Name;


                    // =================================================
                    // Get Grade Wise Deduction Details
                    //
                    // Example:
                    // deductionCode = 1
                    // WHERE dedu_code = 1
                    // =================================================

                    model.DeductionDetails =
                        await _context.DeduFoots
                            .AsNoTracking()
                            .Where(
                                x => x.DeduCode ==
                                     deductionCode.Value)
                            .OrderBy(
                                x => x.GradeCode)
                            .ToListAsync();
                }
            }


            return View(model);
        }
    }
}