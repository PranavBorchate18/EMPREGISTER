using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace Employee_Management_System.Controllers
{
    public class EmployeePaySetupController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmployeePaySetupController(
            ApplicationDbContext context)
        {
            _context = context;
        }


        // =========================================================
        // INDEX
        // =========================================================

        [HttpGet]
        public IActionResult Index()
        {
            return View(
                new EmployeePaySetupViewModel());
        }



        // =========================================================
        // GET EMPLOYEE
        // dbo.PayMast
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> GetEmployee(
            int employeeCode)
        {
            try
            {
                if (employeeCode <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Please enter Employee Code."
                    });
                }


                var employee =
                    await _context.PayMasts
                        .AsNoTracking()
                        .Where(x =>
                            x.EmployeeCode ==
                            employeeCode)
                        .Select(x => new
                        {
                            EmployeeCode =
                                x.EmployeeCode,

                            EmployeeName =
                                x.EmployeeName,

                            BasicSalary =
                                x.BasicSalary,

                            GradeCode =
                                x.GradeId
                        })
                        .FirstOrDefaultAsync();


                if (employee == null)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Employee not found."
                    });
                }


                return Json(new
                {
                    success = true,

                    data = new
                    {
                        employeeCode =
                            employee.EmployeeCode,

                        employeeName =
                            employee.EmployeeName,

                        basicSalary =
                            employee.BasicSalary,

                        gradeCode =
                            employee.GradeCode
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }



        // =========================================================
        // GET GRADE-WISE COMPONENTS
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> GetComponents(
            int employeeCode,
            int flag)
        {
            try
            {
                if (employeeCode <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Please search Employee first."
                    });
                }


                var employee =
                    await _context.PayMasts
                        .AsNoTracking()
                        .Where(x =>
                            x.EmployeeCode ==
                            employeeCode)
                        .Select(x => new
                        {
                            x.EmployeeCode,
                            x.GradeId
                        })
                        .FirstOrDefaultAsync();


                if (employee == null)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Employee not found."
                    });
                }


                if (!employee.GradeId.HasValue)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Employee Grade not found."
                    });
                }


                int gradeCode =
                    employee.GradeId.Value;


                // =================================================
                // ALLOWANCE
                // =================================================

                if (flag == 1)
                {
                    decimal grade =
                        gradeCode;


                    var allowanceCodes =
                        await _context.AllwFoots
                            .AsNoTracking()
                            .Where(x =>
                                x.GradeCode == grade &&
                                x.Apply == true)
                            .Select(x =>
                                x.AllowCode)
                            .Distinct()
                            .ToListAsync();


                    var codes =
                        allowanceCodes
                            .Select(x =>
                                Convert.ToInt32(x))
                            .ToList();


                    if (codes.Count == 0)
                    {
                        return Json(new
                        {
                            success = true,

                            gradeCode,

                            data =
                                Array.Empty<object>(),

                            message =
                                $"No Allowance configured for Grade {gradeCode}."
                        });
                    }


                    var allowances =
                        await _context
                            .AllowanceMasters
                            .AsNoTracking()
                            .Where(x =>
                                codes.Contains(x.Code))
                            .OrderBy(x =>
                                x.Code)
                            .Select(x => new
                            {
                                code =
                                    x.Code,

                                name =
                                    x.Name
                            })
                            .ToListAsync();


                    // Contract Type 6: HRA is not selectable.
                    if (await IsContractEmployeeAsync(employeeCode))
                        allowances = allowances.Where(x => !IsHraName(x.name)).ToList();

                    return Json(new
                    {
                        success = true,
                        gradeCode,
                        data = allowances
                    });
                }


                // =================================================
                // DEDUCTION
                // =================================================

                if (flag == 2)
                {
                    var deductionCodes =
                        await _context.DeduFoots
                            .AsNoTracking()
                            .Where(x =>
                                x.GradeCode ==
                                gradeCode &&
                                x.Apply == true)
                            .Select(x =>
                                x.DeduCode)
                            .Distinct()
                            .ToListAsync();


                    if (deductionCodes.Count == 0)
                    {
                        return Json(new
                        {
                            success = true,

                            gradeCode,

                            data =
                                Array.Empty<object>(),

                            message =
                                $"No Deduction configured for Grade {gradeCode}."
                        });
                    }


                    var deductions =
                        await _context
                            .DeductionMasters
                            .AsNoTracking()
                            .Where(x =>
                                deductionCodes
                                    .Contains(x.Code))
                            .OrderBy(x =>
                                x.Code)
                            .Select(x => new
                            {
                                code =
                                    x.Code,

                                name =
                                    x.Name
                            })
                            .ToListAsync();


                    return Json(new
                    {
                        success = true,
                        gradeCode,
                        data = deductions
                    });
                }


                return Json(new
                {
                    success = false,
                    message =
                        "Please select Allowance or Deduction."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }



        // =========================================================
        // GET CALCULATION RULE
        //
        // Allowance  -> allwfoot
        // Deduction  -> dedufoot
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> GetCalculationRule(
            int employeeCode,
            int flag,
            int componentCode)
        {
            try
            {
                if (employeeCode <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Please search Employee first."
                    });
                }


                if (componentCode <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Please select Component."
                    });
                }


                var employee =
                    await _context.PayMasts
                        .AsNoTracking()
                        .Where(x =>
                            x.EmployeeCode ==
                            employeeCode)
                        .Select(x => new
                        {
                            x.EmployeeCode,
                            x.GradeId
                        })
                        .FirstOrDefaultAsync();


                if (employee == null)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Employee not found."
                    });
                }


                if (!employee.GradeId.HasValue)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Employee Grade not found."
                    });
                }


                if (flag == 1 && await IsContractEmployeeAsync(employeeCode) &&
                    await IsHraAllowanceAsync(componentCode))
                    return Json(new { success = false, message = "HRA is not applicable for Contract (Type 6) employees." });

                int gradeCode =
                    employee.GradeId.Value;


                // =================================================
                // ALLOWANCE
                // =================================================

                if (flag == 1)
                {
                    decimal allowanceCode =
                        componentCode;

                    decimal allowanceGrade =
                        gradeCode;


                    var rule =
                        await _context.AllwFoots
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x =>
                                x.AllowCode ==
                                allowanceCode &&

                                x.GradeCode ==
                                allowanceGrade &&

                                x.Apply == true);


                    if (rule == null)
                    {
                        return Json(new
                        {
                            success = false,

                            message =
                                $"Allowance rule not found for Grade {gradeCode}."
                        });
                    }


                    int calculationCode =
                        rule.CalcCode.HasValue
                            ? Convert.ToInt32(
                                rule.CalcCode.Value)
                            : 0;


                    double rulePercent =
                        rule.Percent.HasValue
                            ? Convert.ToDouble(
                                rule.Percent.Value)
                            : 0;


                    double ruleAmount =
                        rule.FixAmount.HasValue
                            ? Convert.ToDouble(
                                rule.FixAmount.Value)
                            : 0;


                    double percentage = 0;

                    double amount = 0;


                    if (rulePercent != 0)
                    {
                        percentage =
                            rulePercent;

                        amount = 0;
                    }
                    else if (ruleAmount != 0)
                    {
                        percentage = 0;

                        amount =
                            ruleAmount;
                    }


                    return Json(new
                    {
                        success = true,

                        data = new
                        {
                            gradeCode,

                            calculationCode,

                            percentage,

                            amount,

                            minLimit =
                                rule.MinLimit.HasValue
                                    ? Convert.ToDouble(
                                        rule.MinLimit.Value)
                                    : 0,

                            maxLimit =
                                rule.MaxLimit.HasValue
                                    ? Convert.ToDouble(
                                        rule.MaxLimit.Value)
                                    : 0,

                            minDays =
                                rule.MinDays.HasValue
                                    ? Convert.ToDouble(
                                        rule.MinDays.Value)
                                    : 0,

                            payDiv =
                                Convert.ToDouble(
                                    rule.PayDiv),

                            apply =
                                rule.Apply,

                            ruleType =
                                percentage != 0
                                    ? "Percentage"
                                    : amount != 0
                                        ? "FixedAmount"
                                        : "None"
                        }
                    });
                }


                // =================================================
                // DEDUCTION
                // =================================================

                if (flag == 2)
                {
                    var rule =
                        await _context.DeduFoots
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x =>
                                x.DeduCode ==
                                componentCode &&

                                x.GradeCode ==
                                gradeCode &&

                                x.Apply == true);


                    if (rule == null)
                    {
                        return Json(new
                        {
                            success = false,

                            message =
                                $"Deduction rule not found for Grade {gradeCode}."
                        });
                    }


                    int calculationCode =
                        rule.CalcCode.HasValue
                            ? Convert.ToInt32(
                                rule.CalcCode.Value)
                            : 0;


                    double rulePercent =
                        rule.Percent.HasValue
                            ? Convert.ToDouble(
                                rule.Percent.Value)
                            : 0;


                    double ruleAmount =
                        rule.FixAmount.HasValue
                            ? Convert.ToDouble(
                                rule.FixAmount.Value)
                            : 0;


                    double percentage = 0;

                    double amount = 0;


                    if (rulePercent != 0)
                    {
                        percentage =
                            rulePercent;

                        amount = 0;
                    }
                    else if (ruleAmount != 0)
                    {
                        percentage = 0;

                        amount =
                            ruleAmount;
                    }


                    return Json(new
                    {
                        success = true,

                        data = new
                        {
                            gradeCode,

                            calculationCode,

                            percentage,

                            amount,

                            minLimit =
                                rule.MinLimit.HasValue
                                    ? Convert.ToDouble(
                                        rule.MinLimit.Value)
                                    : 0,

                            maxLimit =
                                rule.MaxLimit.HasValue
                                    ? Convert.ToDouble(
                                        rule.MaxLimit.Value)
                                    : 0,

                            minDays =
                                rule.MinDays.HasValue
                                    ? Convert.ToDouble(
                                        rule.MinDays.Value)
                                    : 0,

                            payDiv =
                                (double?)null,

                            apply =
                                rule.Apply,

                            ruleType =
                                percentage != 0
                                    ? "Percentage"
                                    : amount != 0
                                        ? "FixedAmount"
                                        : "None"
                        }
                    });
                }


                return Json(new
                {
                    success = false,
                    message =
                        "Invalid Type."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }



        // =========================================================
        // GET EMPLOYEE PAY COMPONENTS
        // Grade Apply=1 + existing dbo.payfoot values
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> GetEmployeeComponents(int employeeCode)
        {
            try
            {
                if (employeeCode <= 0)
                    return Json(new { success = false, message = "Invalid Employee Code." });

                var employee = await _context.PayMasts
                    .AsNoTracking()
                    .Where(x => x.EmployeeCode == employeeCode)
                    .Select(x => new { x.EmployeeCode, x.GradeId })
                    .FirstOrDefaultAsync();

                if (employee == null)
                    return Json(new { success = false, message = "Employee not found." });

                if (!employee.GradeId.HasValue)
                    return Json(new { success = false, message = "Employee Grade not found." });

                int gradeCode = employee.GradeId.Value;

                // Existing employee-specific rows.
                var existingRows = new List<EmployeePayComponentRow>();
                DbConnection connection = _context.Database.GetDbConnection();
                bool shouldCloseConnection = connection.State != ConnectionState.Open;

                if (shouldCloseConnection)
                    await connection.OpenAsync();

                try
                {
                    using DbCommand command = connection.CreateCommand();
                    command.CommandText = @"
SELECT empl_code, flag, allw_dedu, calc_code, [percent], [date],
       cancel_date, for_times, glc, brnc, slc, loan_adv_amt, amt
FROM dbo.payfoot
WHERE empl_code = @EmployeeCode
ORDER BY flag, allw_dedu";

                    AddParameter(command, "@EmployeeCode", employeeCode);

                    using DbDataReader reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        int? flag = GetNullableInt(reader, "flag");

                        existingRows.Add(new EmployeePayComponentRow
                        {
                            EmployeeCode = GetNullableInt(reader, "empl_code"),
                            Flag = flag,
                            TypeName = flag == 1 ? "Allowance" : flag == 2 ? "Deduction" : "",
                            ComponentCode = GetNullableInt(reader, "allw_dedu"),
                            CalculationCode = GetNullableInt(reader, "calc_code"),
                            Percentage = GetNullableDouble(reader, "percent"),
                            Amount = GetNullableDouble(reader, "amt"),
                            EffectiveDate = GetNullableDateTime(reader, "date"),
                            CancelDate = GetNullableDateTime(reader, "cancel_date"),
                            ForTimes = GetNullableInt(reader, "for_times"),
                            GLC = GetNullableInt(reader, "glc"),
                            Branch = GetNullableInt(reader, "brnc"),
                            SLC = GetNullableInt(reader, "slc"),
                            LoanAdvanceAmount = GetNullableDouble(reader, "loan_adv_amt")
                        });
                    }
                }
                finally
                {
                    if (shouldCloseConnection)
                        await connection.CloseAsync();
                }

                decimal allowanceGrade = gradeCode;

                var allowanceRules = await _context.AllwFoots
                    .AsNoTracking()
                    .Where(x => x.GradeCode == allowanceGrade && x.Apply == true)
                    .OrderBy(x => x.AllowCode)
                    .ToListAsync();

                var deductionRules = await _context.DeduFoots
                    .AsNoTracking()
                    .Where(x => x.GradeCode == gradeCode && x.Apply == true)
                    .OrderBy(x => x.DeduCode)
                    .ToListAsync();

                var allowanceCodes = allowanceRules
                    .Select(x => Convert.ToInt32(x.AllowCode))
                    .Concat(existingRows.Where(x => x.Flag == 1 && x.ComponentCode.HasValue)
                    .Select(x => x.ComponentCode!.Value))
                    .Distinct().ToList();

                var deductionCodes = deductionRules
                    .Select(x => x.DeduCode)
                    .Concat(existingRows.Where(x => x.Flag == 2 && x.ComponentCode.HasValue)
                    .Select(x => x.ComponentCode!.Value))
                    .Distinct().ToList();

                var allowanceNames = await _context.AllowanceMasters
                    .AsNoTracking()
                    .Where(x => allowanceCodes.Contains(x.Code))
                    .ToDictionaryAsync(x => x.Code, x => x.Name ?? "");

                var deductionNames = await _context.DeductionMasters
                    .AsNoTracking()
                    .Where(x => deductionCodes.Contains(x.Code))
                    .ToDictionaryAsync(x => x.Code, x => x.Name ?? "");

                var rows = new List<EmployeePayComponentRow>();

                foreach (var rule in allowanceRules)
                {
                    int code = Convert.ToInt32(rule.AllowCode);
                    var saved = existingRows.FirstOrDefault(x => x.Flag == 1 && x.ComponentCode == code);

                    if (saved != null)
                    {
                        saved.ComponentName = allowanceNames.TryGetValue(code, out string? name) ? name : "";
                        rows.Add(saved);
                    }
                    else
                    {
                        rows.Add(new EmployeePayComponentRow
                        {
                            EmployeeCode = employeeCode,
                            Flag = 1,
                            TypeName = "Allowance",
                            ComponentCode = code,
                            ComponentName = allowanceNames.TryGetValue(code, out string? name) ? name : "",
                            CalculationCode = rule.CalcCode.HasValue ? Convert.ToInt32(rule.CalcCode.Value) : 0,
                            // No payfoot row yet: employee-specific values start at 0.
                            Percentage =
                                rule.Percent.HasValue
                                    ? Convert.ToDouble(
                                        rule.Percent.Value)
                                    : 0,

                            Amount =
                                rule.FixAmount.HasValue
                                    ? Convert.ToDouble(
                                        rule.FixAmount.Value)
                                    : 0,

                            EffectiveDate = null,
                            CancelDate = null,
                            ForTimes = 0,
                            GLC = 0,
                            Branch = 0,
                            SLC = 0,
                            LoanAdvanceAmount = 0
                        });
                    }
                }

                foreach (var rule in deductionRules)
                {
                    int code = rule.DeduCode;
                    var saved = existingRows.FirstOrDefault(x => x.Flag == 2 && x.ComponentCode == code);

                    if (saved != null)
                    {
                        saved.ComponentName = deductionNames.TryGetValue(code, out string? name) ? name : "";
                        rows.Add(saved);
                    }
                    else
                    {
                        rows.Add(new EmployeePayComponentRow
                        {
                            EmployeeCode = employeeCode,
                            Flag = 2,
                            TypeName = "Deduction",
                            ComponentCode = code,
                            ComponentName = deductionNames.TryGetValue(code, out string? name) ? name : "",
                            CalculationCode = rule.CalcCode.HasValue ? Convert.ToInt32(rule.CalcCode.Value) : 0,
                            Percentage =
                                rule.Percent.HasValue
                                    ? Convert.ToDouble(
                                        rule.Percent.Value)
                                    : 0,

                            Amount =
                                rule.FixAmount.HasValue
                                    ? Convert.ToDouble(
                                        rule.FixAmount.Value)
                                    : 0,

                            EffectiveDate = null,
                            CancelDate = null,
                            ForTimes = 0,
                            GLC = 0,
                            Branch = 0,
                            SLC = 0,
                            LoanAdvanceAmount = 0
                        });
                    }
                }

                // Preserve an already-saved legacy payfoot component even if
                // it is no longer present in the current grade rule.
                foreach (var saved in existingRows)
                {
                    if (!saved.Flag.HasValue || !saved.ComponentCode.HasValue)
                        continue;

                    if (rows.Any(x => x.Flag == saved.Flag &&
                                      x.ComponentCode == saved.ComponentCode))
                        continue;

                    int code = saved.ComponentCode.Value;

                    if (saved.Flag == 1)
                        saved.ComponentName = allowanceNames.TryGetValue(code, out string? name) ? name : "";
                    else if (saved.Flag == 2)
                        saved.ComponentName = deductionNames.TryGetValue(code, out string? name) ? name : "";

                    rows.Add(saved);
                }

                // Contract Type 6: hide HRA including previously saved legacy payfoot rows.
                if (await IsContractEmployeeAsync(employeeCode))
                    rows = rows.Where(x => !(x.Flag == 1 && IsHraName(x.ComponentName))).ToList();

                rows = rows.OrderBy(x => x.Flag).ThenBy(x => x.ComponentCode).ToList();

                if (await IsContractEmployeeAsync(employeeCode))
                {
                    foreach (var pfRow in rows.Where(x => x.Flag == 2 && x.ComponentCode.HasValue &&
                        (x.ComponentName ?? "").Trim().ToUpper() is "PF" or "PROVIDENT FUND" or "P.F." or "P.F"))
                    {
                        pfRow.Percentage = 0;
                        pfRow.Amount = 0;
                    }
                }

                return Json(new { success = true, gradeCode, data = rows });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }



        // =========================================================
        // CANCEL / DELETE PAY COMPONENT
        //
        // Deletes ONLY the employee-specific row from dbo.payfoot.
        //
        // flag = 1 -> Allowance
        // flag = 2 -> Deduction
        //
        // KEY:
        // empl_code + flag + allw_dedu
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelComponent(
            int employeeCode,
            int flag,
            int componentCode)
        {
            try
            {
                if (employeeCode <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Invalid Employee Code."
                    });
                }


                if (flag != 1 &&
                    flag != 2)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Invalid Component Type."
                    });
                }


                if (componentCode <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Invalid Component Code."
                    });
                }


                DbConnection connection =
                    _context.Database
                        .GetDbConnection();


                bool shouldCloseConnection =
                    connection.State !=
                    ConnectionState.Open;


                if (shouldCloseConnection)
                {
                    await connection.OpenAsync();
                }


                try
                {
                    using DbCommand command =
                        connection.CreateCommand();


                    command.CommandText = @"
DELETE FROM dbo.payfoot
WHERE empl_code = @EmployeeCode
  AND flag = @Flag
  AND allw_dedu = @ComponentCode";


                    AddParameter(
                        command,
                        "@EmployeeCode",
                        employeeCode);


                    AddParameter(
                        command,
                        "@Flag",
                        flag);


                    AddParameter(
                        command,
                        "@ComponentCode",
                        componentCode);


                    int rowsAffected =
                        await command
                            .ExecuteNonQueryAsync();


                    if (rowsAffected <= 0)
                    {
                        return Json(new
                        {
                            success = false,
                            message =
                                "Component record not found in dbo.payfoot."
                        });
                    }


                    string typeName =
                        flag == 1
                            ? "Allowance"
                            : "Deduction";


                    return Json(new
                    {
                        success = true,
                        message =
                            $"{typeName} cancelled successfully."
                    });
                }
                finally
                {
                    if (shouldCloseConnection)
                    {
                        await connection
                            .CloseAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }



        // =========================================================
        // UPDATE ALL COMPONENTS
        //
        // Bottom Update button calls this method.
        // It reads the Employee Grade and stores every Apply = 1
        // Allowance + Deduction in dbo.payfoot.
        //
        // Existing payfoot row -> NO duplicate INSERT
        // Missing payfoot row  -> INSERT grade rule values
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAllComponents(
            int employeeCode)
        {
            try
            {
                if (employeeCode <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Please search Employee first."
                    });
                }


                var employee =
                    await _context.PayMasts
                        .AsNoTracking()
                        .Where(x =>
                            x.EmployeeCode ==
                            employeeCode)
                        .Select(x => new
                        {
                            x.EmployeeCode,
                            x.GradeId
                        })
                        .FirstOrDefaultAsync();


                if (employee == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Employee not found."
                    });
                }


                if (!employee.GradeId.HasValue)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Employee Grade not found."
                    });
                }


                int gradeCode =
                    employee.GradeId.Value;


                decimal allowanceGrade =
                    gradeCode;


                var allowanceRules =
                    await _context.AllwFoots
                        .AsNoTracking()
                        .Where(x =>
                            x.GradeCode ==
                            allowanceGrade &&
                            x.Apply == true)
                        .OrderBy(x =>
                            x.AllowCode)
                        .ToListAsync();


                if (await IsContractEmployeeAsync(employeeCode))
                {
                    var hraCodes = await _context.AllowanceMasters.AsNoTracking()
                        .Where(x => x.Name != null &&
                            (x.Name.Trim().ToUpper() == "HRA" ||
                             x.Name.Trim().ToUpper() == "HOUSE RENT ALLOWANCE"))
                        .Select(x => x.Code).ToListAsync();
                    allowanceRules = allowanceRules
                        .Where(x => !hraCodes.Contains(Convert.ToInt32(x.AllowCode))).ToList();
                }

                var deductionRules =
                    await _context.DeduFoots
                        .AsNoTracking()
                        .Where(x =>
                            x.GradeCode ==
                            gradeCode &&
                            x.Apply == true)
                        .OrderBy(x =>
                            x.DeduCode)
                        .ToListAsync();


                if (allowanceRules.Count == 0 &&
                    deductionRules.Count == 0)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            $"No Allowance or Deduction configured for Grade {gradeCode}."
                    });
                }


                int insertedCount =
                    await EnsureGradeComponentsInPayFoot(
                        employeeCode,
                        allowanceRules,
                        deductionRules);


                return Json(new
                {
                    success = true,
                    gradeCode = gradeCode,
                    insertedCount = insertedCount,
                    allowanceCount =
                        allowanceRules.Count,
                    deductionCount =
                        deductionRules.Count,
                    message =
                        "All Allowance and Deduction updated successfully."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }



        // =========================================================
        // SAVE / UPDATE PAY SETUP
        //
        // Existing row -> UPDATE
        // New row      -> INSERT
        //
        // KEY:
        // empl_code + flag + allw_dedu
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(
            EmployeePaySetupViewModel model)
        {
            try
            {
                // =================================================
                // VALIDATION
                // =================================================

                if (!model.EmployeeCode.HasValue ||
                    model.EmployeeCode.Value <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Employee Code is required."
                    });
                }


                if (model.Flag != 1 &&
                    model.Flag != 2)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Please select Allowance or Deduction."
                    });
                }


                if (!model.ComponentCode.HasValue ||
                    model.ComponentCode.Value <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Please select Component."
                    });
                }


                // =================================================
                // EMPLOYEE + GRADE
                // =================================================

                var employee =
                    await _context.PayMasts
                        .AsNoTracking()
                        .Where(x =>
                            x.EmployeeCode ==
                            model.EmployeeCode.Value)
                        .Select(x => new
                        {
                            x.EmployeeCode,
                            x.GradeId
                        })
                        .FirstOrDefaultAsync();


                if (employee == null)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Employee not found."
                    });
                }


                if (!employee.GradeId.HasValue)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Employee Grade not found."
                    });
                }


                if (model.Flag == 1 && model.ComponentCode.HasValue &&
                    await IsContractEmployeeAsync(model.EmployeeCode.Value) &&
                    await IsHraAllowanceAsync(model.ComponentCode.Value))
                    return Json(new { success = false, message = "HRA is not applicable for Contract (Type 6) employees." });

                if (model.Flag == 2 && model.ComponentCode.HasValue &&
                    await IsContractEmployeeAsync(model.EmployeeCode.Value) &&
                    await IsProvidentFundAsync(model.ComponentCode.Value))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Provident Fund is not applicable for Contract (Type 6) employees. PF deduction is 0."
                    });
                }

                int gradeCode =
                    employee.GradeId.Value;


                int calculationCode = 0;

                double percentage = 0;

                double amount = 0;


                // =================================================
                // ALLOWANCE RULE
                // =================================================

                if (model.Flag == 1)
                {
                    decimal allowCode =
                        model.ComponentCode.Value;

                    decimal grade =
                        gradeCode;


                    var rule =
                        await _context.AllwFoots
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x =>
                                x.AllowCode ==
                                allowCode &&

                                x.GradeCode ==
                                grade &&

                                x.Apply == true);


                    if (rule == null)
                    {
                        return Json(new
                        {
                            success = false,
                            message =
                                "Allowance calculation rule not found."
                        });
                    }


                    calculationCode =
                        rule.CalcCode.HasValue
                            ? Convert.ToInt32(
                                rule.CalcCode.Value)
                            : 0;


                    double rulePercent =
                        rule.Percent.HasValue
                            ? Convert.ToDouble(
                                rule.Percent.Value)
                            : 0;


                    double ruleAmount =
                        rule.FixAmount.HasValue
                            ? Convert.ToDouble(
                                rule.FixAmount.Value)
                            : 0;


                    // Percentage rule
                    // Employee-specific percentage must be
                    // >= 0 and strictly less than rule percentage.

                    if (rulePercent != 0)
                    {
                        double employeePercentage =
                            model.Percentage ?? 0;

                        if (employeePercentage < 0)
                        {
                            return Json(new
                            {
                                success = false,
                                message =
                                    "Percentage cannot be less than 0."
                            });
                        }



                        percentage =
                            employeePercentage;

                        amount = 0;
                    }

                    // Fixed amount rule

                    else if (ruleAmount != 0)
                    {
                        double employeeAmount = model.Amount ?? 0;

                        if (employeeAmount < 0)
                            return Json(new { success = false, message = "Amount cannot be less than 0." });



                        percentage = 0;
                        amount = employeeAmount;
                    }

                    else
                    {
                        percentage = 0;
                        amount = 0;
                    }
                }


                // =================================================
                // DEDUCTION RULE
                // =================================================

                else if (model.Flag == 2)
                {
                    var rule =
                        await _context.DeduFoots
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x =>
                                x.DeduCode ==
                                model.ComponentCode.Value &&

                                x.GradeCode ==
                                gradeCode &&

                                x.Apply == true);


                    if (rule == null)
                    {
                        return Json(new
                        {
                            success = false,
                            message =
                                "Deduction calculation rule not found."
                        });
                    }


                    calculationCode =
                        rule.CalcCode.HasValue
                            ? Convert.ToInt32(
                                rule.CalcCode.Value)
                            : 0;


                    double rulePercent =
                        rule.Percent.HasValue
                            ? Convert.ToDouble(
                                rule.Percent.Value)
                            : 0;


                    double ruleAmount =
                        rule.FixAmount.HasValue
                            ? Convert.ToDouble(
                                rule.FixAmount.Value)
                            : 0;


                    if (rulePercent != 0)
                    {
                        double employeePercentage =
                            model.Percentage ?? 0;

                        if (employeePercentage < 0)
                        {
                            return Json(new
                            {
                                success = false,
                                message =
                                    "Percentage cannot be less than 0."
                            });
                        }



                        percentage =
                            employeePercentage;

                        amount = 0;
                    }
                    else if (ruleAmount != 0)
                    {
                        double employeeAmount = model.Amount ?? 0;

                        if (employeeAmount < 0)
                            return Json(new { success = false, message = "Amount cannot be less than 0." });



                        percentage = 0;
                        amount = employeeAmount;
                    }
                    else
                    {
                        percentage = 0;
                        amount = 0;
                    }
                }


                // =================================================
                // EMPLOYEE SPECIFIC VALUES
                // =================================================

                int forTimes =
                    model.ForTimes ?? 0;

                int glc =
                    model.GLC ?? 0;

                int branch =
                    model.Branch ?? 0;

                int slc =
                    model.SLC ?? 0;

                double loanAdvanceAmount =
                    model.LoanAdvanceAmount ?? 0;


                // =================================================
                // CONNECTION
                // =================================================

                DbConnection connection =
                    _context.Database
                        .GetDbConnection();


                bool shouldCloseConnection =
                    connection.State !=
                    ConnectionState.Open;


                if (shouldCloseConnection)
                {
                    await connection
                        .OpenAsync();
                }


                try
                {
                    bool recordExists;


                    // =============================================
                    // CHECK EXISTING RECORD
                    // =============================================

                    using (
                        DbCommand checkCommand =
                            connection
                                .CreateCommand())
                    {
                        checkCommand.CommandText = @"
SELECT COUNT(1)
FROM dbo.payfoot
WHERE empl_code = @EmployeeCode
  AND flag = @Flag
  AND allw_dedu = @ComponentCode";


                        AddParameter(
                            checkCommand,
                            "@EmployeeCode",
                            model.EmployeeCode.Value);


                        AddParameter(
                            checkCommand,
                            "@Flag",
                            model.Flag.Value);


                        AddParameter(
                            checkCommand,
                            "@ComponentCode",
                            model.ComponentCode.Value);


                        object? result =
                            await checkCommand
                                .ExecuteScalarAsync();


                        recordExists =
                            Convert.ToInt32(
                                result ?? 0) > 0;
                    }


                    // =============================================
                    // UPDATE EXISTING RECORD
                    // =============================================

                    if (recordExists)
                    {
                        using DbCommand command =
                            connection.CreateCommand();


                        command.CommandText = @"
UPDATE dbo.payfoot
SET
    calc_code = @CalculationCode,
    [percent] = @Percentage,
    [date] = @EffectiveDate,
    cancel_date = @CancelDate,
    for_times = @ForTimes,
    glc = @GLC,
    brnc = @Branch,
    slc = @SLC,
    loan_adv_amt = @LoanAdvanceAmount,
    amt = @Amount,
    Entry_Date = @EntryDate,
    Opn_By = @OpenedBy,
    Opn_IP = @OpenedIP
WHERE empl_code = @EmployeeCode
  AND flag = @Flag
  AND allw_dedu = @ComponentCode";


                        AddPayFootParameters(
                            command,
                            model,
                            calculationCode,
                            percentage,
                            amount,
                            forTimes,
                            glc,
                            branch,
                            slc,
                            loanAdvanceAmount);


                        int rowsAffected =
                            await command
                                .ExecuteNonQueryAsync();


                        if (rowsAffected <= 0)
                        {
                            return Json(new
                            {
                                success = false,
                                message =
                                    "Employee Pay Setup could not be updated."
                            });
                        }


                        return Json(new
                        {
                            success = true,
                            action = "update",

                            message =
                                "Employee Pay Setup updated successfully."
                        });
                    }


                    // =============================================
                    // INSERT NEW RECORD
                    // =============================================

                    using (
                        DbCommand command =
                            connection.CreateCommand())
                    {
                        command.CommandText = @"
INSERT INTO dbo.payfoot
(
    empl_code,
    flag,
    allw_dedu,
    calc_code,
    [percent],
    [date],
    cancel_date,
    for_times,
    glc,
    brnc,
    slc,
    loan_adv_amt,
    amt,
    Entry_Date,
    Opn_By,
    Opn_IP
)
VALUES
(
    @EmployeeCode,
    @Flag,
    @ComponentCode,
    @CalculationCode,
    @Percentage,
    @EffectiveDate,
    @CancelDate,
    @ForTimes,
    @GLC,
    @Branch,
    @SLC,
    @LoanAdvanceAmount,
    @Amount,
    @EntryDate,
    @OpenedBy,
    @OpenedIP
)";


                        AddPayFootParameters(
                            command,
                            model,
                            calculationCode,
                            percentage,
                            amount,
                            forTimes,
                            glc,
                            branch,
                            slc,
                            loanAdvanceAmount);


                        int rowsAffected =
                            await command
                                .ExecuteNonQueryAsync();


                        if (rowsAffected <= 0)
                        {
                            return Json(new
                            {
                                success = false,
                                message =
                                    "Employee Pay Setup could not be saved."
                            });
                        }
                    }


                    return Json(new
                    {
                        success = true,
                        action = "insert",

                        message =
                            "Employee Pay Setup saved successfully."
                    });
                }
                finally
                {
                    if (shouldCloseConnection)
                    {
                        await connection
                            .CloseAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }



        // =========================================================
        // PAYFOOT PARAMETERS
        // Used by INSERT + UPDATE
        // =========================================================

        private void AddPayFootParameters(
            DbCommand command,
            EmployeePaySetupViewModel model,
            int calculationCode,
            double percentage,
            double amount,
            int forTimes,
            int glc,
            int branch,
            int slc,
            double loanAdvanceAmount)
        {
            AddParameter(
                command,
                "@EmployeeCode",
                model.EmployeeCode!.Value);


            AddParameter(
                command,
                "@Flag",
                model.Flag!.Value);


            AddParameter(
                command,
                "@ComponentCode",
                model.ComponentCode!.Value);


            AddParameter(
                command,
                "@CalculationCode",
                calculationCode);


            AddParameter(
                command,
                "@Percentage",
                percentage);


            AddParameter(
                command,
                "@Amount",
                amount);


            AddParameter(
                command,
                "@EffectiveDate",
                model.EffectiveDate);


            AddParameter(
                command,
                "@CancelDate",
                model.CancelDate);


            AddParameter(
                command,
                "@ForTimes",
                forTimes);


            AddParameter(
                command,
                "@GLC",
                glc);


            AddParameter(
                command,
                "@Branch",
                branch);


            AddParameter(
                command,
                "@SLC",
                slc);


            AddParameter(
                command,
                "@LoanAdvanceAmount",
                loanAdvanceAmount);


            AddParameter(
                command,
                "@EntryDate",
                DateTime.Now);


            AddParameter(
                command,
                "@OpenedBy",
                User?.Identity?.Name
                ?? "Admin");


            AddParameter(
                command,
                "@OpenedIP",
                HttpContext.Connection
                    .RemoteIpAddress?
                    .ToString()
                ?? "");
        }



        // =========================================================
        // ENSURE ALL GRADE COMPONENTS EXIST IN dbo.payfoot
        // =========================================================

        private async Task<int> EnsureGradeComponentsInPayFoot(
            int employeeCode,
            List<AllwFoot> allowanceRules,
            List<DeduFoot> deductionRules)
        {
            int insertedCount = 0;

            DbConnection connection =
                _context.Database
                    .GetDbConnection();


            bool shouldCloseConnection =
                connection.State !=
                ConnectionState.Open;


            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }


            try
            {
                string openedBy =
                    User?.Identity?.Name
                    ?? "Admin";


                string openedIp =
                    HttpContext.Connection
                        .RemoteIpAddress?
                        .ToString()
                    ?? "";


                DateTime entryDate =
                    DateTime.Now;


                // ---------------------------------------------
                // ALLOWANCE
                // ---------------------------------------------

                foreach (var rule in allowanceRules)
                {
                    int componentCode =
                        Convert.ToInt32(
                            rule.AllowCode);


                    int calculationCode =
                        rule.CalcCode.HasValue
                            ? Convert.ToInt32(
                                rule.CalcCode.Value)
                            : 0;


                    double percentage =
                        rule.Percent.HasValue
                            ? Convert.ToDouble(
                                rule.Percent.Value)
                            : 0;


                    double amount =
                        rule.FixAmount.HasValue
                            ? Convert.ToDouble(
                                rule.FixAmount.Value)
                            : 0;


                    using DbCommand command =
                        connection.CreateCommand();


                    command.CommandText = @"
INSERT INTO dbo.payfoot
(
    empl_code,
    flag,
    allw_dedu,
    calc_code,
    [percent],
    [date],
    cancel_date,
    for_times,
    glc,
    brnc,
    slc,
    loan_adv_amt,
    amt,
    Entry_Date,
    Opn_By,
    Opn_IP
)
SELECT
    @EmployeeCode,
    1,
    @ComponentCode,
    @CalculationCode,
    @Percentage,
    NULL,
    NULL,
    0,
    0,
    0,
    0,
    0,
    @Amount,
    @EntryDate,
    @OpenedBy,
    @OpenedIP
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.payfoot
    WHERE empl_code = @EmployeeCode
      AND flag = 1
      AND allw_dedu = @ComponentCode
)";


                    AddParameter(
                        command,
                        "@EmployeeCode",
                        employeeCode);

                    AddParameter(
                        command,
                        "@ComponentCode",
                        componentCode);

                    AddParameter(
                        command,
                        "@CalculationCode",
                        calculationCode);

                    AddParameter(
                        command,
                        "@Percentage",
                        percentage);

                    AddParameter(
                        command,
                        "@Amount",
                        amount);

                    AddParameter(
                        command,
                        "@EntryDate",
                        entryDate);

                    AddParameter(
                        command,
                        "@OpenedBy",
                        openedBy);

                    AddParameter(
                        command,
                        "@OpenedIP",
                        openedIp);


                    insertedCount +=
                        await command.ExecuteNonQueryAsync();
                }


                // ---------------------------------------------
                // DEDUCTION
                // ---------------------------------------------

                foreach (var rule in deductionRules)
                {
                    int componentCode =
                        rule.DeduCode;


                    int calculationCode =
                        rule.CalcCode.HasValue
                            ? Convert.ToInt32(
                                rule.CalcCode.Value)
                            : 0;


                    double percentage =
                        rule.Percent.HasValue
                            ? Convert.ToDouble(
                                rule.Percent.Value)
                            : 0;


                    double amount =
                        rule.FixAmount.HasValue
                            ? Convert.ToDouble(
                                rule.FixAmount.Value)
                            : 0;


                    using DbCommand command =
                        connection.CreateCommand();


                    command.CommandText = @"
INSERT INTO dbo.payfoot
(
    empl_code,
    flag,
    allw_dedu,
    calc_code,
    [percent],
    [date],
    cancel_date,
    for_times,
    glc,
    brnc,
    slc,
    loan_adv_amt,
    amt,
    Entry_Date,
    Opn_By,
    Opn_IP
)
SELECT
    @EmployeeCode,
    2,
    @ComponentCode,
    @CalculationCode,
    @Percentage,
    NULL,
    NULL,
    0,
    0,
    0,
    0,
    0,
    @Amount,
    @EntryDate,
    @OpenedBy,
    @OpenedIP
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.payfoot
    WHERE empl_code = @EmployeeCode
      AND flag = 2
      AND allw_dedu = @ComponentCode
)";


                    AddParameter(
                        command,
                        "@EmployeeCode",
                        employeeCode);

                    AddParameter(
                        command,
                        "@ComponentCode",
                        componentCode);

                    AddParameter(
                        command,
                        "@CalculationCode",
                        calculationCode);

                    AddParameter(
                        command,
                        "@Percentage",
                        percentage);

                    AddParameter(
                        command,
                        "@Amount",
                        amount);

                    AddParameter(
                        command,
                        "@EntryDate",
                        entryDate);

                    AddParameter(
                        command,
                        "@OpenedBy",
                        openedBy);

                    AddParameter(
                        command,
                        "@OpenedIP",
                        openedIp);


                    insertedCount +=
                        await command.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }
            }


            return insertedCount;
        }



        // =========================================================
        // DATABASE HELPERS
        // =========================================================

        private static int? GetNullableInt(
            DbDataReader reader,
            string columnName)
        {
            int ordinal =
                reader.GetOrdinal(
                    columnName);


            if (reader.IsDBNull(
                ordinal))
            {
                return null;
            }


            return Convert.ToInt32(
                reader.GetValue(
                    ordinal));
        }


        private static double? GetNullableDouble(
            DbDataReader reader,
            string columnName)
        {
            int ordinal =
                reader.GetOrdinal(
                    columnName);


            if (reader.IsDBNull(
                ordinal))
            {
                return null;
            }


            return Convert.ToDouble(
                reader.GetValue(
                    ordinal));
        }


        private static DateTime?
            GetNullableDateTime(
                DbDataReader reader,
                string columnName)
        {
            int ordinal =
                reader.GetOrdinal(
                    columnName);


            if (reader.IsDBNull(
                ordinal))
            {
                return null;
            }


            return Convert.ToDateTime(
                reader.GetValue(
                    ordinal));
        }



        // Contract employees (dbo.paymast.type = 6) must not have PF deducted.
        private async Task<bool> IsContractEmployeeAsync(int employeeCode)
        {
            var connection = _context.Database.GetDbConnection();
            bool close = connection.State != ConnectionState.Open;
            try
            {
                if (close) await connection.OpenAsync();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"SELECT CASE WHEN TRY_CONVERT(INT,[type]) = 6 THEN 1 ELSE 0 END
FROM dbo.paymast WHERE TRY_CONVERT(INT,code) = @EmployeeCode";
                AddParameter(cmd, "@EmployeeCode", employeeCode);
                return Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0) == 1;
            }
            finally
            {
                if (close && connection.State == ConnectionState.Open) await connection.CloseAsync();
            }
        }

        private static bool IsHraName(string? name) =>
            string.Equals(name?.Trim(), "HRA", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name?.Trim(), "HOUSE RENT ALLOWANCE", StringComparison.OrdinalIgnoreCase);

        private async Task<bool> IsHraAllowanceAsync(int componentCode) =>
            await _context.AllowanceMasters.AsNoTracking().AnyAsync(x =>
                x.Code == componentCode && x.Name != null &&
                (x.Name.Trim().ToUpper() == "HRA" ||
                 x.Name.Trim().ToUpper() == "HOUSE RENT ALLOWANCE"));

        private async Task<bool> IsProvidentFundAsync(int componentCode)
        {
            return await _context.DeductionMasters.AsNoTracking().AnyAsync(x =>
                x.Code == componentCode && x.Name != null &&
                (x.Name.Trim().ToUpper() == "PF" ||
                 x.Name.Trim().ToUpper() == "PROVIDENT FUND" ||
                 x.Name.Trim().ToUpper() == "P.F." ||
                 x.Name.Trim().ToUpper() == "P.F"));
        }

        private static void AddParameter(
            DbCommand command,
            string parameterName,
            object? value)
        {
            DbParameter parameter =
                command.CreateParameter();


            parameter.ParameterName =
                parameterName;


            parameter.Value =
                value ?? DBNull.Value;


            command.Parameters.Add(
                parameter);
        }
    }
}