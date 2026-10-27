
using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace Employee_Management_System.Controllers
{
    public class IncomeTaxController : Controller
    {
        private readonly ApplicationDbContext _context;

        public IncomeTaxController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Form16(
            int? employeeCode,
            int? financialYear)
        {
            if (string.IsNullOrWhiteSpace(
                HttpContext.Session.GetString("LoginId")))
            {
                return RedirectToAction("Login", "Account");
            }

            int fy = financialYear ??
                (DateTime.Today.Month >= 4
                    ? DateTime.Today.Year
                    : DateTime.Today.Year - 1);

            if (fy < 2000 || fy > 2100)
            {
                return BadRequest("Invalid financial year.");
            }

            var vm = new IncomeTaxViewModel
            {
                FinancialYear = fy,
                EmployeeCode = employeeCode ?? 0
            };

            var connection = _context.Database.GetDbConnection();
            bool openedHere = connection.State != ConnectionState.Open;

            try
            {
                if (openedHere)
                    await connection.OpenAsync();

                // Employee master list
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
SELECT
    TRY_CONVERT(INT, code) AS EmployeeCode,
    ISNULL(name, '') AS EmployeeName
FROM dbo.paymast
WHERE TRY_CONVERT(INT, code) IS NOT NULL
ORDER BY TRY_CONVERT(INT, code);";

                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        int code = Convert.ToInt32(reader["EmployeeCode"]);
                        string name = Convert.ToString(
                            reader["EmployeeName"]) ?? "";

                        vm.Employees.Add(new SelectListItem
                        {
                            Value = code.ToString(),
                            Text = $"{code} - {name}",
                            Selected = code == vm.EmployeeCode
                        });
                    }
                }

                if (vm.EmployeeCode > 0)
                {
                    // Read employee name and PAN.
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
SELECT TOP (1)
    ISNULL(name, '') AS EmployeeName,
    ISNULL(Pan_No, '') AS PAN
FROM dbo.paymast
WHERE TRY_CONVERT(INT, code) = @EmployeeCode;";

                        AddParameter(
                            cmd, "@EmployeeCode", vm.EmployeeCode);

                        using var reader = await cmd.ExecuteReaderAsync();

                        if (!await reader.ReadAsync())
                            return NotFound("Employee not found.");

                        vm.EmployeeName =
                            Convert.ToString(reader["EmployeeName"]) ?? "";

                        vm.PAN =
                            Convert.ToString(reader["PAN"]) ?? "";
                    }

                    // Financial year: April to March.
                    // Gross salary = basic + allowances.
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
SELECT
    COUNT(*) AS SalaryMonths,
    COALESCE(SUM(
        COALESCE(TRY_CONVERT(DECIMAL(18,2), basic), 0)
    ), 0) AS AnnualBasic,
    COALESCE(SUM(
        COALESCE(TRY_CONVERT(DECIMAL(18,2), tot_allw), 0)
    ), 0) AS AnnualAllowance
FROM dbo.paytrn
WHERE TRY_CONVERT(INT, code) = @EmployeeCode
  AND (
      TRY_CONVERT(INT, [year]) * 12
      + TRY_CONVERT(INT, [month])
  ) BETWEEN (@FinancialYear * 12 + 4)
      AND ((@FinancialYear + 1) * 12 + 3);";

                        AddParameter(
                            cmd, "@EmployeeCode", vm.EmployeeCode);

                        AddParameter(
                            cmd, "@FinancialYear", fy);

                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            int months = Convert.ToInt32(
                                reader["SalaryMonths"]);

                            vm.AnnualBasic = Convert.ToDecimal(
                                reader["AnnualBasic"]);

                            vm.AnnualAllowance = Convert.ToDecimal(
                                reader["AnnualAllowance"]);

                            vm.AnnualGrossSalary =
                                vm.AnnualBasic + vm.AnnualAllowance;

                            vm.SalaryDataAvailable = months > 0;
                        }
                    }
                }

                return View(vm);
            }
            catch (Exception)
            {
                // Log the actual exception in your application's
                // configured logger before deploying to production.
                throw;
            }
            finally
            {
                if (openedHere && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }

        [HttpGet]
        public IActionResult Form26AS()
        {
            if (string.IsNullOrWhiteSpace(
                HttpContext.Session.GetString("LoginId")))
            {
                return RedirectToAction("Login", "Account");
            }

            return View();
        }

        private static void AddParameter(
            DbCommand command,
            string name,
            object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
    }
}
