using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace Employee_Management_System.Controllers
{
    public class GradeWiseAllowDeductionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GradeWiseAllowDeductionController(
            ApplicationDbContext context)
        {
            _context = context;
        }


        // ============================================================
        // INDEX
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            int? allowanceCode,
            int? deductionCode,
            string? section)
        {
            var model =
                new GradeWiseAllowDeductionViewModel();


            // ========================================================
            // NAVBAR SECTION
            //
            // section=allowance -> show only Allowance
            // section=deduction -> show only Deduction
            // ========================================================
            section =
                (section ?? "").Trim().ToLowerInvariant();


            if (section != "allowance" &&
                section != "deduction")
            {
                // Backward compatibility:
                // if page is opened using old URL, detect section
                // from the selected code.
                if (deductionCode.HasValue)
                {
                    section = "deduction";
                }
                else
                {
                    section = "allowance";
                }
            }


            ViewBag.SelectedSection =
                section;


            // ========================================================
            // ALLOWANCE MASTER LIST
            // dbo.allwmast
            // ========================================================
            model.Allowances =
                await _context.AllowanceMasters
                    .AsNoTracking()
                    .OrderBy(x => x.Code)
                    .ToListAsync();


            // ========================================================
            // DEDUCTION MASTER LIST
            // dbo.dedumast
            // ========================================================
            model.Deductions =
                await _context.DeductionMasters
                    .AsNoTracking()
                    .OrderBy(x => x.Code)
                    .ToListAsync();


            // ========================================================
            // GRADE MASTER
            // dbo.gradmast
            // ========================================================
            model.Grades =
                await _context.Grades
                    .AsNoTracking()
                    .OrderBy(x => x.Code)
                    .ToListAsync();


            // ========================================================
            // APPLIED GRADE COUNT FROM dbo.allwfoot
            // One row = one applied grade for that allowance
            // ========================================================
            var allowanceCountRows =
                await _context.AllwFoots
                    .AsNoTracking()
                    .Where(x => x.Apply == true)
                    .GroupBy(x => x.AllowCode)
                    .Select(g => new
                    {
                        AllowanceCode = g.Key,
                        AppliedCount = g.Count()
                    })
                    .ToListAsync();

            model.AllowanceSelectedGradeCounts =
                allowanceCountRows.ToDictionary(
                    x => Convert.ToInt32(x.AllowanceCode),
                    x => x.AppliedCount);


            // ========================================================
            // APPLIED GRADE COUNT FROM dbo.dedufoot
            // One row = one applied grade for that deduction
            // ========================================================
            var deductionCountRows =
                await _context.DeduFoots
                    .AsNoTracking()
                    .Where(x => x.Apply == true)
                    .GroupBy(x => x.DeduCode)
                    .Select(g => new
                    {
                        DeductionCode = g.Key,
                        AppliedCount = g.Count()
                    })
                    .ToListAsync();

            model.DeductionSelectedGradeCounts =
                deductionCountRows.ToDictionary(
                    x => x.DeductionCode,
                    x => x.AppliedCount);


            // ========================================================
            // SELECTED ALLOWANCE
            //
            // allwmast.code       = allwfoot.allw_code
            // allwfoot.grad_code = gradmast.code
            // ========================================================
            if (allowanceCode.HasValue)
            {
                var selectedAllowance =
                    await _context.AllowanceMasters
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            x => x.Code ==
                                 allowanceCode.Value);


                if (selectedAllowance != null)
                {
                    model.SelectedAllowanceCode =
                        selectedAllowance.Code;

                    model.SelectedAllowanceName =
                        selectedAllowance.Name;


                    decimal selectedCode =
                        selectedAllowance.Code;


                    model.AllowanceDetails =
                        await
                        (
                            from g in _context.Grades.AsNoTracking()

                            join a in
                                _context.AllwFoots
                                    .AsNoTracking()
                                    .Where(x => x.AllowCode == selectedCode)
                            on (decimal)g.Code equals a.GradeCode
                            into gradeAllowanceRows

                            from a in gradeAllowanceRows.DefaultIfEmpty()

                            orderby
                                (a != null && a.Apply == true) ? 0 : 1,
                                g.Code

                            select new GradeWiseAllowanceDetailViewModel
                            {
                                // Grade Code + Name from dbo.gradmast
                                Code = g.Code,
                                Name = g.GradeName,

                                // dbo.allwfoot.Apply = 1 means applied
                                IsSelected =
                                    a != null &&
                                    a.Apply == true,

                                PayDiv = a != null ? a.PayDiv : 0,
                                FixAmount = a != null ? a.FixAmount : null,
                                CalcCode = a != null ? a.CalcCode : null,
                                Percent = a != null ? a.Percent : null,
                                MinLimit = a != null ? a.MinLimit : null,
                                MaxLimit = a != null ? a.MaxLimit : null,
                                MinDays = a != null ? a.MinDays : null
                            }

                        ).ToListAsync();

                    // Formula is available for every allowance.
                    // It is stored directly in dbo.allwfoot.formula.
                    var formulas = await LoadAllowanceFormulasAsync(selectedCode);
                    foreach (var row in model.AllowanceDetails)
                    {
                        if (formulas.TryGetValue(row.Code, out var formula))
                            row.Formula = formula;
                    }
                }
            }


            // ========================================================
            // SELECTED DEDUCTION
            //
            // dedumast.code       = dedufoot.dedu_code
            // dedufoot.grad_code = gradmast.code
            //
            // Grade Code + Grade Name are fetched from dbo.gradmast.
            // ========================================================
            if (deductionCode.HasValue)
            {
                var selectedDeduction =
                    await _context.DeductionMasters
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            x => x.Code ==
                                 deductionCode.Value);


                if (selectedDeduction != null)
                {
                    model.SelectedDeductionCode =
                        selectedDeduction.Code;

                    model.SelectedDeductionName =
                        selectedDeduction.Name;


                    int selectedCode =
                        selectedDeduction.Code;


                    model.DeductionDetails =
                        await
                        (
                            from g in _context.Grades.AsNoTracking()

                            join d in
                                _context.DeduFoots
                                    .AsNoTracking()
                                    .Where(x => x.DeduCode == selectedCode)
                            on g.Code equals d.GradeCode
                            into gradeDeductionRows

                            from d in gradeDeductionRows.DefaultIfEmpty()

                            orderby
                                (d != null && d.Apply == true) ? 0 : 1,
                                g.Code

                            select new GradeWiseDeductionDetailViewModel
                            {
                                Code = g.Code,
                                Name = g.GradeName,

                                // dbo.dedufoot.Apply = 1 means applied
                                IsSelected =
                                    d != null &&
                                    d.Apply == true,

                                FixAmount = d != null ? d.FixAmount : null,
                                CalcCode = d != null ? d.CalcCode : null,
                                Percent = d != null ? d.Percent : null,
                                MinLimit = d != null ? d.MinLimit : null,
                                MaxLimit = d != null ? d.MaxLimit : null,
                                MinDays = d != null ? d.MinDays : null
                            }

                        ).ToListAsync();
                }
            }


            return View(model);
        }



        // ============================================================
        // INSERT ALLOWANCE DETAIL
        // dbo.allwfoot
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            InsertAllowanceDetail(
                int allowanceCode,
                int gradeCode,
                decimal payDiv,
                decimal? fixAmount,
                decimal? calcCode,
                decimal? percent,
                decimal? minLimit,
                decimal? maxLimit,
                decimal? minDays,
                string? formula = null)
        {
            try
            {
                if (allowanceCode <= 0)
                {
                    TempData["Error"] =
                        "Allowance Code is required.";

                    return RedirectToAction(
                        nameof(Index));
                }


                if (gradeCode <= 0)
                {
                    TempData["Error"] =
                        "Grade Code is required.";

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            allowanceCode,
                            section = "allowance"
                        });
                }


                decimal selectedAllowanceCode =
                    allowanceCode;

                decimal selectedGradeCode =
                    gradeCode;


                bool allowanceExists =
                    await _context.AllowanceMasters
                        .AsNoTracking()
                        .AnyAsync(
                            x => x.Code ==
                                 allowanceCode);


                if (!allowanceExists)
                {
                    TempData["Error"] =
                        "Allowance not found.";

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            allowanceCode,
                            section = "allowance"
                        });
                }


                bool gradeExists =
                    await _context.Grades
                        .AsNoTracking()
                        .AnyAsync(
                            x => x.Code ==
                                 gradeCode);


                if (!gradeExists)
                {
                    TempData["Error"] =
                        "Grade not found.";

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            allowanceCode,
                            section = "allowance"
                        });
                }


                bool alreadyExists =
                    await _context.AllwFoots
                        .AsNoTracking()
                        .AnyAsync(
                            x =>
                                x.AllowCode ==
                                selectedAllowanceCode
                                &&
                                x.GradeCode ==
                                selectedGradeCode);


                if (alreadyExists)
                {
                    TempData["Error"] =
                        "This Grade is already added " +
                        "for the selected Allowance.";

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            allowanceCode,
                            section = "allowance"
                        });
                }


                var detail =
                    new AllwFoot
                    {
                        AllowCode =
                            selectedAllowanceCode,

                        GradeCode =
                            selectedGradeCode,

                        PayDiv =
                            payDiv,

                        FixAmount =
                            fixAmount,

                        CalcCode =
                            calcCode,

                        Percent =
                            percent,

                        MinLimit =
                            minLimit,

                        MaxLimit =
                            maxLimit,

                        MinDays =
                            minDays,

                        Apply =
                            true
                    };


                _context.AllwFoots.Add(detail);

                await _context.SaveChangesAsync();

                await SaveAllowanceFormulaAsync(
                    selectedAllowanceCode, selectedGradeCode, formula);


                TempData["Success"] =
                    "Allowance grade detail inserted successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "Unable to insert allowance detail. " +
                    ex.Message;
            }


            return RedirectToAction(
                nameof(Index),
                new
                {
                    allowanceCode,
                    section = "allowance"
                });
        }



        // ============================================================
        // SAVE / UPDATE ALLOWANCE DETAIL
        //
        // Grade checkbox select only shows Edit button.
        // Actual dbo.allwfoot row is inserted/updated after Save.
        // Apply BIT:
        // selected + saved = 1
        // unselected       = 0
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            UpdateAllowanceDetail(
                int allowanceCode,
                int gradeCode,
                decimal payDiv,
                decimal? fixAmount,
                decimal? calcCode,
                decimal? percent,
                decimal? minLimit,
                decimal? maxLimit,
                decimal? minDays,
                string? formula = null)
        {
            try
            {
                if (allowanceCode <= 0 ||
                    gradeCode <= 0)
                {
                    TempData["Error"] =
                        "Invalid Allowance or Grade Code.";

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            allowanceCode,
                            section = "allowance"
                        });
                }


                decimal selectedAllowanceCode =
                    allowanceCode;

                decimal selectedGradeCode =
                    gradeCode;


                bool allowanceExists =
                    await _context.AllowanceMasters
                        .AsNoTracking()
                        .AnyAsync(
                            x =>
                                x.Code ==
                                allowanceCode);


                if (!allowanceExists)
                {
                    TempData["Error"] =
                        "Allowance not found.";

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            allowanceCode,
                            section = "allowance"
                        });
                }


                bool gradeExists =
                    await _context.Grades
                        .AsNoTracking()
                        .AnyAsync(
                            x =>
                                x.Code ==
                                gradeCode);


                if (!gradeExists)
                {
                    TempData["Error"] =
                        "Grade not found.";

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            allowanceCode,
                            section = "allowance"
                        });
                }


                var detail =
                    await _context.AllwFoots
                        .FirstOrDefaultAsync(
                            x =>
                                x.AllowCode ==
                                selectedAllowanceCode
                                &&
                                x.GradeCode ==
                                selectedGradeCode);


                bool isNewRow =
                    detail == null;


                if (detail == null)
                {
                    detail =
                        new AllwFoot
                        {
                            AllowCode =
                                selectedAllowanceCode,

                            GradeCode =
                                selectedGradeCode
                        };

                    _context.AllwFoots.Add(
                        detail
                    );
                }


                // Only values posted from the UI are saved.
                // Nullable blank fields remain NULL.
                detail.PayDiv =
                    payDiv;

                detail.FixAmount =
                    fixAmount;

                detail.CalcCode =
                    calcCode;

                detail.Percent =
                    percent;

                detail.MinLimit =
                    minLimit;

                detail.MaxLimit =
                    maxLimit;

                detail.MinDays =
                    minDays;

                // Selected Grade saved by Admin.
                // SQL BIT -> 1
                detail.Apply =
                    true;


                await _context.SaveChangesAsync();

                // Save formula for the selected allowance + grade. Blank means no special formula.
                await SaveAllowanceFormulaAsync(
                    selectedAllowanceCode, selectedGradeCode, formula);


                TempData["Success"] =
                    isNewRow
                        ? "Allowance grade detail saved successfully."
                        : "Allowance grade detail updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "Unable to save allowance detail. " +
                    ex.Message;
            }


            return RedirectToAction(
                nameof(Index),
                new
                {
                    allowanceCode,
                    section = "allowance"
                });
        }



        // ============================================================
        // UNSELECT ALLOWANCE GRADE
        //
        // Existing row is NOT deleted.
        // Apply = false -> SQL BIT 0.
        // All previously entered values remain in dbo.allwfoot.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            UpdateAllowanceApply(
                int allowanceCode,
                int gradeCode,
                bool isSelected)
        {
            try
            {
                decimal selectedAllowanceCode =
                    allowanceCode;

                decimal selectedGradeCode =
                    gradeCode;


                var detail =
                    await _context.AllwFoots
                        .FirstOrDefaultAsync(
                            x =>
                                x.AllowCode ==
                                selectedAllowanceCode
                                &&
                                x.GradeCode ==
                                selectedGradeCode);


                // If row does not exist yet, checkbox selection is only
                // a UI state until Admin opens Edit and saves values.
                if (detail == null)
                {
                    return Json(
                        new
                        {
                            success = true,
                            rowExists = false,
                            apply = false
                        });
                }


                detail.Apply =
                    isSelected;


                await _context.SaveChangesAsync();


                int selectedCount =
                    await _context.AllwFoots
                        .AsNoTracking()
                        .CountAsync(
                            x =>
                                x.AllowCode ==
                                selectedAllowanceCode
                                &&
                                x.Apply == true);


                return Json(
                    new
                    {
                        success = true,
                        rowExists = true,
                        apply = detail.Apply,
                        selectedCount
                    });
            }
            catch (Exception ex)
            {
                return Json(
                    new
                    {
                        success = false,
                        message =
                            ex.Message
                    });
            }
        }



        // ============================================================
        // INSERT DEDUCTION DETAIL
        // dbo.dedufoot
        //
        // Saved Grade => Apply = true => SQL BIT 1
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            InsertDeductionDetail(
                int deductionCode,
                int gradeCode,
                decimal? fixAmount,
                short? calcCode,
                decimal? percent,
                decimal? minLimit,
                decimal? maxLimit,
                int? minDays)
        {
            try
            {
                if (deductionCode <= 0)
                {
                    TempData["Error"] =
                        "Deduction Code is required.";

                    return RedirectToAction(
                        nameof(Index));
                }

                if (gradeCode <= 0)
                {
                    TempData["Error"] =
                        "Grade Code is required.";

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            deductionCode,
                            section = "deduction"
                        });
                }

                bool deductionExists =
                    await _context.DeductionMasters
                        .AsNoTracking()
                        .AnyAsync(
                            x => x.Code ==
                                 deductionCode);

                if (!deductionExists)
                {
                    TempData["Error"] =
                        "Deduction not found.";

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            deductionCode,
                            section = "deduction"
                        });
                }

                bool gradeExists =
                    await _context.Grades
                        .AsNoTracking()
                        .AnyAsync(
                            x => x.Code ==
                                 gradeCode);

                if (!gradeExists)
                {
                    TempData["Error"] =
                        "Grade not found.";

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            deductionCode,
                            section = "deduction"
                        });
                }

                var detail =
                    await _context.DeduFoots
                        .FirstOrDefaultAsync(
                            x =>
                                x.DeduCode == deductionCode
                                &&
                                x.GradeCode == gradeCode);

                if (detail != null)
                {
                    TempData["Error"] =
                        "This Grade is already added " +
                        "for the selected Deduction.";

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            deductionCode,
                            section = "deduction"
                        });
                }

                detail =
                    new DeduFoot
                    {
                        DeduCode = deductionCode,
                        GradeCode = gradeCode,
                        FixAmount = fixAmount,
                        CalcCode = calcCode,
                        Percent = percent,
                        MinLimit = minLimit,
                        MaxLimit = maxLimit,
                        MinDays = minDays,
                        Apply = true
                    };

                _context.DeduFoots.Add(detail);

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Deduction grade detail inserted successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "Unable to insert deduction detail. " +
                    ex.Message;
            }

            return RedirectToAction(
                nameof(Index),
                new
                {
                    deductionCode,
                    section = "deduction"
                });
        }



        // ============================================================
        // SAVE / UPDATE DEDUCTION DETAIL
        //
        // Checkbox select only shows Edit button.
        // Actual dbo.dedufoot row is inserted/updated after Save.
        // Apply BIT:
        // selected + saved = 1
        // unselected       = 0
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            UpdateDeductionDetail(
                int deductionCode,
                int gradeCode,
                decimal? fixAmount,
                short? calcCode,
                decimal? percent,
                decimal? minLimit,
                decimal? maxLimit,
                int? minDays)
        {
            try
            {
                if (deductionCode <= 0 ||
                    gradeCode <= 0)
                {
                    TempData["Error"] =
                        "Invalid Deduction or Grade Code.";

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            deductionCode,
                            section = "deduction"
                        });
                }

                bool deductionExists =
                    await _context.DeductionMasters
                        .AsNoTracking()
                        .AnyAsync(
                            x => x.Code ==
                                 deductionCode);

                if (!deductionExists)
                {
                    TempData["Error"] =
                        "Deduction not found.";

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            deductionCode,
                            section = "deduction"
                        });
                }

                bool gradeExists =
                    await _context.Grades
                        .AsNoTracking()
                        .AnyAsync(
                            x => x.Code ==
                                 gradeCode);

                if (!gradeExists)
                {
                    TempData["Error"] =
                        "Grade not found.";

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            deductionCode,
                            section = "deduction"
                        });
                }

                var detail =
                    await _context.DeduFoots
                        .FirstOrDefaultAsync(
                            x =>
                                x.DeduCode == deductionCode
                                &&
                                x.GradeCode == gradeCode);

                bool isNewRow =
                    detail == null;

                if (detail == null)
                {
                    detail =
                        new DeduFoot
                        {
                            DeduCode = deductionCode,
                            GradeCode = gradeCode
                        };

                    _context.DeduFoots.Add(detail);
                }

                // Only values posted from the UI are saved.
                // Blank nullable values remain NULL.
                detail.FixAmount = fixAmount;
                detail.CalcCode = calcCode;
                detail.Percent = percent;
                detail.MinLimit = minLimit;
                detail.MaxLimit = maxLimit;
                detail.MinDays = minDays;

                // Selected Grade saved by Admin.
                // SQL BIT -> 1
                detail.Apply = true;

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    isNewRow
                        ? "Deduction grade detail saved successfully."
                        : "Deduction grade detail updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "Unable to save deduction detail. " +
                    ex.Message;
            }

            return RedirectToAction(
                nameof(Index),
                new
                {
                    deductionCode,
                    section = "deduction"
                });
        }



        // ============================================================
        // SELECT / UNSELECT DEDUCTION GRADE
        //
        // Existing row is NOT deleted.
        // Apply = false -> SQL BIT 0.
        // Previously entered values remain in dbo.dedufoot.
        // If row does not exist, checking is UI-only until Edit -> Save.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            UpdateDeductionApply(
                int deductionCode,
                int gradeCode,
                bool isSelected)
        {
            try
            {
                var detail =
                    await _context.DeduFoots
                        .FirstOrDefaultAsync(
                            x =>
                                x.DeduCode == deductionCode
                                &&
                                x.GradeCode == gradeCode);

                if (detail == null)
                {
                    return Json(
                        new
                        {
                            success = true,
                            rowExists = false,
                            apply = false
                        });
                }

                detail.Apply =
                    isSelected;

                await _context.SaveChangesAsync();

                int selectedCount =
                    await _context.DeduFoots
                        .AsNoTracking()
                        .CountAsync(
                            x =>
                                x.DeduCode == deductionCode
                                &&
                                x.Apply == true);

                return Json(
                    new
                    {
                        success = true,
                        rowExists = true,
                        apply = detail.Apply,
                        selectedCount
                    });
            }
            catch (Exception ex)
            {
                return Json(
                    new
                    {
                        success = false,
                        message = ex.Message
                    });
            }
        }



        // ============================================================
        // DELETE DEDUCTION DETAIL
        // dbo.dedufoot
        // Find by dedu_code + grad_code
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            DeleteDeductionDetail(
                int deductionCode,
                int gradeCode)
        {
            try
            {
                if (deductionCode <= 0 ||
                    gradeCode <= 0)
                {
                    TempData["Error"] =
                        "Invalid Deduction or Grade Code.";

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            deductionCode,
                            section = "deduction"
                        });
                }


                int affectedRows =
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $@"
                            DELETE FROM dbo.dedufoot
                            WHERE
                                dedu_code = {deductionCode}
                                AND
                                grad_code = {gradeCode}"
                    );


                if (affectedRows == 0)
                {
                    TempData["Error"] =
                        "Deduction grade detail not found.";
                }
                else
                {
                    TempData["Success"] =
                        "Deduction grade detail deleted successfully.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "Unable to delete deduction detail. " +
                    ex.Message;
            }


            return RedirectToAction(
                nameof(Index),
                new
                {
                    deductionCode,
                    section = "deduction"
                });
        }

        // ============================================================
        // ALLOWANCE FORMULA HELPERS
        // Formula is available for every allowance and is read/written with raw SQL because
        // dbo.allwfoot.formula was added directly in SQL Server.
        // No AllwFoot entity change is required.
        // ============================================================
        private async Task<Dictionary<int, string>> LoadAllowanceFormulasAsync(decimal allowanceCode)
        {
            var result = new Dictionary<int, string>();
            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;

            try
            {
                if (close) await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
SELECT TRY_CONVERT(INT, grad_code) AS GradeCode,
       ISNULL(CONVERT(NVARCHAR(MAX), formula), '') AS Formula
FROM dbo.allwfoot
WHERE TRY_CONVERT(DECIMAL(18,4), allw_code) = @AllowanceCode;";

                AddDbParameter(command, "@AllowanceCode", allowanceCode);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (reader["GradeCode"] == DBNull.Value) continue;
                    int gradeCode = Convert.ToInt32(reader["GradeCode"]);
                    string formula = Convert.ToString(reader["Formula"])?.Trim() ?? string.Empty;
                    result[gradeCode] = formula;
                }
            }
            finally
            {
                if (close && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }

            return result;
        }

        private async Task SaveAllowanceFormulaAsync(
            decimal allowanceCode,
            decimal gradeCode,
            string? formula)
        {
            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;

            try
            {
                if (close) await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
UPDATE dbo.allwfoot
SET formula = @Formula
WHERE TRY_CONVERT(DECIMAL(18,4), allw_code) = @AllowanceCode
  AND TRY_CONVERT(DECIMAL(18,4), grad_code) = @GradeCode;";

                AddDbParameter(command, "@Formula",
                    string.IsNullOrWhiteSpace(formula) ? DBNull.Value : formula.Trim());
                AddDbParameter(command, "@AllowanceCode", allowanceCode);
                AddDbParameter(command, "@GradeCode", gradeCode);

                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                if (close && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }

        private static void AddDbParameter(DbCommand command, string name, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

    }
}
