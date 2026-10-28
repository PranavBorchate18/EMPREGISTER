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

            DateTime fromDate = new DateTime(fy, 4, 1);
            DateTime toDate = new DateTime(fy + 1, 3, 31);

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

                // Employee list (type = 1, same as legacy report)
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
SELECT TRY_CONVERT(INT, code) AS EmployeeCode,
       ISNULL(name, '') AS EmployeeName
FROM dbo.paymast
WHERE TRY_CONVERT(INT, code) IS NOT NULL
  AND TRY_CONVERT(INT, [type]) = 1
ORDER BY TRY_CONVERT(INT, code);";

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int code = Convert.ToInt32(reader["EmployeeCode"]);
                        string name = Convert.ToString(reader["EmployeeName"]) ?? "";

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
                    // Employee header
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
SELECT TOP (1)
    ISNULL(p.name, '')   AS EmployeeName,
    ISNULL(p.Pan_No, '') AS PAN,
    CASE WHEN p.Aadhar_No IS NULL THEN ''
         ELSE CONVERT(VARCHAR(20), CONVERT(DECIMAL(18,0), p.Aadhar_No)) END AS UID,
    ISNULL(b.name, '')   AS BranchName
FROM dbo.paymast p
LEFT JOIN dbo.brncmast b
    ON TRY_CONVERT(INT, b.code) = TRY_CONVERT(INT, p.brnc_code)
WHERE TRY_CONVERT(INT, p.code) = @EmployeeCode;";
                        AddParameter(cmd, "@EmployeeCode", vm.EmployeeCode);

                        using var reader = await cmd.ExecuteReaderAsync();
                        if (!await reader.ReadAsync())
                            return NotFound("Employee not found.");

                        vm.EmployeeName = Convert.ToString(reader["EmployeeName"])?.Trim() ?? "";
                        vm.PAN = Convert.ToString(reader["PAN"])?.Trim() ?? "";
                        vm.UID = Convert.ToString(reader["UID"])?.Trim() ?? "";
                        vm.BranchName = Convert.ToString(reader["BranchName"])?.Trim() ?? "";
                    }

                    // 12 month rows, April .. March
                    var rows = new Dictionary<(int Y, int M), IncomeTaxMonthRow>();
                    for (int i = 0; i < 12; i++)
                    {
                        var d = fromDate.AddMonths(i);
                        rows[(d.Year, d.Month)] = new IncomeTaxMonthRow
                        {
                            Year = d.Year,
                            Month = d.Month
                        };
                    }

                    // Months that have a paytrn row (type = 1) -> basic
                    var payrollMonths = new HashSet<(int, int)>();
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
SELECT TRY_CONVERT(INT, [year])  AS Y,
       TRY_CONVERT(INT, [month]) AS M,
       SUM(ISNULL(TRY_CONVERT(FLOAT, basic), 0)) AS Basic
FROM dbo.paytrn
WHERE TRY_CONVERT(INT, code) = @Emp
  AND TRY_CONVERT(INT, [type]) = 1
  AND (TRY_CONVERT(INT, [year]) * 12 + TRY_CONVERT(INT, [month]))
      BETWEEN (@FY * 12 + 4) AND ((@FY + 1) * 12 + 3)
GROUP BY TRY_CONVERT(INT, [year]), TRY_CONVERT(INT, [month]);";
                        AddParameter(cmd, "@Emp", vm.EmployeeCode);
                        AddParameter(cmd, "@FY", fy);

                        using var reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            var key = (Convert.ToInt32(reader["Y"]), Convert.ToInt32(reader["M"]));
                            if (!rows.TryGetValue(key, out var row)) continue;
                            payrollMonths.Add(key);
                            row.Basic += Dec(reader["Basic"]);
                        }
                    }

                    // paytrnft allowances (flag = 1), only for months present in paytrn
                    //   code 1 -> basic, code 4 -> HRA, others -> Other Allowance
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
SELECT TRY_CONVERT(INT, ft.[year])  AS Y,
       TRY_CONVERT(INT, ft.[month]) AS M,
       TRY_CONVERT(INT, ft.allw_dedu_code) AS Code,
       SUM(ISNULL(TRY_CONVERT(FLOAT, ft.amt), 0)) AS Amt
FROM dbo.paytrnft ft
WHERE TRY_CONVERT(INT, ft.empl) = @Emp
  AND TRY_CONVERT(INT, ft.[type]) = 1
  AND TRY_CONVERT(INT, ft.flag) = 1
  AND (TRY_CONVERT(INT, ft.[year]) * 12 + TRY_CONVERT(INT, ft.[month]))
      BETWEEN (@FY * 12 + 4) AND ((@FY + 1) * 12 + 3)
  AND EXISTS (
        SELECT 1 FROM dbo.paytrn t
        WHERE TRY_CONVERT(INT, t.code)   = @Emp
          AND TRY_CONVERT(INT, t.[type]) = 1
          AND TRY_CONVERT(INT, t.[year])  = TRY_CONVERT(INT, ft.[year])
          AND TRY_CONVERT(INT, t.[month]) = TRY_CONVERT(INT, ft.[month]))
GROUP BY TRY_CONVERT(INT, ft.[year]), TRY_CONVERT(INT, ft.[month]),
         TRY_CONVERT(INT, ft.allw_dedu_code);";
                        AddParameter(cmd, "@Emp", vm.EmployeeCode);
                        AddParameter(cmd, "@FY", fy);

                        using var reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            var key = (Convert.ToInt32(reader["Y"]), Convert.ToInt32(reader["M"]));
                            if (!rows.TryGetValue(key, out var row)) continue;

                            int code = reader["Code"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Code"]);
                            decimal amt = Dec(reader["Amt"]);

                            if (code == 1) row.Basic += amt;
                            else if (code == 4) row.HRA += amt;
                            else row.OtherAllowance += amt;
                        }
                    }

                    // Incentive_Trn, month-wise by Post_Date
                    // 1 = Prize, 2 = Gold, 3 = Recovery, 4 = Bonus
                    // This table exists only in the legacy database. If it is
                    // missing here, those four columns stay 0 and the page
                    // shows a notice instead of crashing.
                    using (var check = connection.CreateCommand())
                    {
                        check.CommandText = "SELECT OBJECT_ID(N'dbo.Incentive_Trn', N'U');";
                        var id = await check.ExecuteScalarAsync();
                        vm.IncentiveAvailable = id != null && id != DBNull.Value;
                    }

                    if (vm.IncentiveAvailable)
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.CommandText = @"
SELECT YEAR(Post_Date)  AS Y,
       MONTH(Post_Date) AS M,
       TRY_CONVERT(INT, Incentive_Code) AS Code,
       SUM(ISNULL(TRY_CONVERT(FLOAT, Amount), 0)) AS Amt
FROM dbo.Incentive_Trn
WHERE TRY_CONVERT(INT, emp_Code) = @Emp
  AND TRY_CONVERT(INT, Incentive_Code) IN (1, 2, 3, 4)
  AND Post_YN = 'Y'
  AND Post_Date >= @From AND Post_Date <= @To
GROUP BY YEAR(Post_Date), MONTH(Post_Date), TRY_CONVERT(INT, Incentive_Code);";
                        AddParameter(cmd, "@Emp", vm.EmployeeCode);
                        AddParameter(cmd, "@From", fromDate);
                        AddParameter(cmd, "@To", toDate);

                        using var reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            var key = (Convert.ToInt32(reader["Y"]), Convert.ToInt32(reader["M"]));
                            if (!rows.TryGetValue(key, out var row)) continue;

                            decimal amt = Dec(reader["Amt"]);
                            switch (Convert.ToInt32(reader["Code"]))
                            {
                                case 1: row.Prize += amt; break;
                                case 2: row.GoldAllowance += amt; break;
                                case 3: row.RecoveryAllowance += amt; break;
                                case 4: row.Bonus += amt; break;
                            }
                        }
                    }

                    vm.Months = rows.OrderBy(r => r.Key.Y).ThenBy(r => r.Key.M)
                                    .Select(r => r.Value).ToList();

                    // Salary totals for the Form 16 view.
                    // These are based only on the salary/allowance rows loaded above.
                    // Exemptions, tax deductions and TDS are not inferred here because
                    // their source columns/codes have not yet been confirmed.
                    vm.GrossSalary = vm.GrandTotal;

                    // Legacy rule: show only if (basic + other allowance) != 0
                    vm.SalaryDataAvailable =
                        payrollMonths.Count > 0 &&
                        (vm.TotalBasic + vm.TotalOther) != 0;
                }

                return View(vm);
            }
            finally
            {
                if (openedHere && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }

        private static decimal Dec(object value) =>
            value == null || value == DBNull.Value
                ? 0m
                : Math.Round(Convert.ToDecimal(value), 2);

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