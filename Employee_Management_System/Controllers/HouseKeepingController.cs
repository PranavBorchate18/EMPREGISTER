
using Employee_Management_System.Data;
using Employee_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace Employee_Management_System.Controllers
{
    public class HouseKeepingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HouseKeepingController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            return View(new HouseKeepingViewModel
            {
                SelectedYear = DateTime.Now.Year,
                SelectedMonth = DateTime.Now.Month,
                SelectedEmployeeType = 0
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetBranches(
            int year,
            int month,
            int employeeType)
        {
            if (!IsLoggedIn())
            {
                return Json(new
                {
                    success = false,
                    sessionExpired = true,
                    message = "Login session expired. Please login again."
                });
            }

            if (!IsValidFilter(year, month, employeeType))
            {
                return Json(new
                {
                    success = false,
                    message = "Please select a valid Year, Month and Employee Type."
                });
            }

            var connection = _context.Database.GetDbConnection();
            bool closeConnection = connection.State != ConnectionState.Open;

            try
            {
                if (closeConnection)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();

                command.CommandText = @"
SELECT
    TRY_CONVERT(INT, b.code) AS BranchCode,
    ISNULL(b.name, '') AS BranchName,

    COUNT(DISTINCT TRY_CONVERT(INT, t.code)) AS EmployeeCount,

    ISNULL(SUM(ISNULL(TRY_CONVERT(FLOAT, t.basic), 0)), 0) AS TotalBasic,

    ISNULL(SUM(ISNULL(TRY_CONVERT(FLOAT, t.tot_allw), 0)), 0) AS TotalAllowance,

    ISNULL(SUM(ISNULL(TRY_CONVERT(FLOAT, t.tot_dedu), 0)), 0) AS TotalDeduction,

    ISNULL(SUM(
        ISNULL(TRY_CONVERT(FLOAT, t.basic), 0) +
        ISNULL(TRY_CONVERT(FLOAT, t.tot_allw), 0) -
        ISNULL(TRY_CONVERT(FLOAT, t.tot_dedu), 0)
    ), 0) AS TotalNetSalary

FROM dbo.brncmast b

LEFT JOIN dbo.paytrn t
    ON TRY_CONVERT(INT, t.brnc) = TRY_CONVERT(INT, b.code)
    AND TRY_CONVERT(INT, t.[year]) = @Year
    AND TRY_CONVERT(INT, t.[month]) = @Month
    AND
    (
        @EmployeeType = 0
        OR (@EmployeeType = 1 AND TRY_CONVERT(INT, t.[type]) IN (1,2,3,4,5))
        OR (@EmployeeType = 6 AND TRY_CONVERT(INT, t.[type]) = 6)
        OR (@EmployeeType = 7 AND TRY_CONVERT(INT, t.[type]) = 7)
        OR (@EmployeeType = 8 AND TRY_CONVERT(INT, t.[type]) = 8)
        OR (@EmployeeType = 9 AND TRY_CONVERT(INT, t.[type]) = 9)
        OR (@EmployeeType = 10 AND TRY_CONVERT(INT, t.[type]) = 10)
    )

WHERE TRY_CONVERT(INT, b.code) IS NOT NULL

GROUP BY b.code, b.name

ORDER BY TRY_CONVERT(INT, b.code);";

                AddParameter(command, "@Year", year);
                AddParameter(command, "@Month", month);
                AddParameter(command, "@EmployeeType", employeeType);

                var branches = new List<HouseKeepingBranchViewModel>();

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (reader["BranchCode"] == DBNull.Value)
                        continue;

                    branches.Add(new HouseKeepingBranchViewModel
                    {
                        BranchCode = Convert.ToInt32(reader["BranchCode"]),
                        BranchName = Convert.ToString(reader["BranchName"])?.Trim() ?? "",
                        EmployeeCount = Convert.ToInt32(reader["EmployeeCount"]),
                        TotalBasic = Number(reader["TotalBasic"]),
                        TotalAllowance = Number(reader["TotalAllowance"]),
                        TotalDeduction = Number(reader["TotalDeduction"]),
                        TotalNetSalary = Number(reader["TotalNetSalary"])
                    });
                }

                return Json(new
                {
                    success = true,
                    data = branches,
                    totalBranches = branches.Count,
                    totalEmployees = branches.Sum(x => x.EmployeeCount),
                    totalBasic = branches.Sum(x => x.TotalBasic),
                    totalAllowance = branches.Sum(x => x.TotalAllowance),
                    totalDeduction = branches.Sum(x => x.TotalDeduction),
                    totalNetSalary = branches.Sum(x => x.TotalNetSalary)
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Unable to load Branch List. " + ex.Message
                });
            }
            finally
            {
                if (closeConnection && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveBranch(
            int year,
            int month,
            int employeeType,
            int branchCode)
        {
            if (!IsLoggedIn())
            {
                return Json(new
                {
                    success = false,
                    sessionExpired = true,
                    message = "Login session expired."
                });
            }

            if (branchCode <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "Invalid Branch."
                });
            }

            return await RemovePayroll(year, month, employeeType, branchCode);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAll(
            int year,
            int month,
            int employeeType)
        {
            return await RemovePayroll(year, month, employeeType, null);
        }

        private async Task<IActionResult> RemovePayroll(
            int year,
            int month,
            int employeeType,
            int? branchCode)
        {
            if (!IsLoggedIn())
            {
                return Json(new
                {
                    success = false,
                    sessionExpired = true,
                    message = "Login session expired."
                });
            }

            if (!IsValidFilter(year, month, employeeType))
            {
                return Json(new
                {
                    success = false,
                    message = "Invalid Year, Month or Employee Type."
                });
            }

            var connection = _context.Database.GetDbConnection();

            bool closeConnection = connection.State != ConnectionState.Open;

            DbTransaction? transaction = null;

            try
            {
                if (closeConnection)
                    await connection.OpenAsync();

                transaction = await connection.BeginTransactionAsync();

                // Count Final Posted records. These records are never deleted.
                using var postedCommand = connection.CreateCommand();

                postedCommand.Transaction = transaction;

                postedCommand.CommandText = @"
SELECT COUNT(1)
FROM dbo.paytrn t
WHERE TRY_CONVERT(INT, t.[year]) = @Year
  AND TRY_CONVERT(INT, t.[month]) = @Month
  AND UPPER(LTRIM(RTRIM(ISNULL(t.POST_FLAG, '')))) = 'Y'
  AND (@BranchCode IS NULL OR TRY_CONVERT(INT, t.brnc) = @BranchCode)
  AND
  (
        @EmployeeType = 0
        OR (@EmployeeType = 1 AND TRY_CONVERT(INT, t.[type]) IN (1,2,3,4,5))
        OR (@EmployeeType = 6 AND TRY_CONVERT(INT, t.[type]) = 6)
        OR (@EmployeeType = 7 AND TRY_CONVERT(INT, t.[type]) = 7)
        OR (@EmployeeType = 8 AND TRY_CONVERT(INT, t.[type]) = 8)
        OR (@EmployeeType = 9 AND TRY_CONVERT(INT, t.[type]) = 9)
        OR (@EmployeeType = 10 AND TRY_CONVERT(INT, t.[type]) = 10)
  );";

                AddParameter(postedCommand, "@Year", year);
                AddParameter(postedCommand, "@Month", month);
                AddParameter(postedCommand, "@EmployeeType", employeeType);
                AddParameter(postedCommand, "@BranchCode", branchCode);

                int protectedPosted = Convert.ToInt32(
                    await postedCommand.ExecuteScalarAsync() ?? 0);

                // Delete payroll detail rows first.
                using var detailCommand = connection.CreateCommand();

                detailCommand.Transaction = transaction;

                detailCommand.CommandText = @"
DELETE ft
FROM dbo.paytrnft ft
WHERE TRY_CONVERT(INT, ft.[year]) = @Year
  AND TRY_CONVERT(INT, ft.[month]) = @Month
  AND EXISTS
  (
      SELECT 1
      FROM dbo.paytrn t
      WHERE TRY_CONVERT(INT, t.[year]) = TRY_CONVERT(INT, ft.[year])
        AND TRY_CONVERT(INT, t.[month]) = TRY_CONVERT(INT, ft.[month])
        AND TRY_CONVERT(INT, t.code) = TRY_CONVERT(INT, ft.empl)
        AND TRY_CONVERT(INT, t.[type]) = TRY_CONVERT(INT, ft.[type])
        AND TRY_CONVERT(INT, t.[year]) = @Year
        AND TRY_CONVERT(INT, t.[month]) = @Month
        AND UPPER(LTRIM(RTRIM(ISNULL(t.POST_FLAG, '')))) <> 'Y'
        AND (@BranchCode IS NULL OR TRY_CONVERT(INT, t.brnc) = @BranchCode)
        AND
        (
              @EmployeeType = 0
              OR (@EmployeeType = 1 AND TRY_CONVERT(INT, t.[type]) IN (1,2,3,4,5))
              OR (@EmployeeType = 6 AND TRY_CONVERT(INT, t.[type]) = 6)
              OR (@EmployeeType = 7 AND TRY_CONVERT(INT, t.[type]) = 7)
              OR (@EmployeeType = 8 AND TRY_CONVERT(INT, t.[type]) = 8)
              OR (@EmployeeType = 9 AND TRY_CONVERT(INT, t.[type]) = 9)
              OR (@EmployeeType = 10 AND TRY_CONVERT(INT, t.[type]) = 10)
        )
  );";

                AddParameter(detailCommand, "@Year", year);
                AddParameter(detailCommand, "@Month", month);
                AddParameter(detailCommand, "@EmployeeType", employeeType);
                AddParameter(detailCommand, "@BranchCode", branchCode);

                int deletedDetails = await detailCommand.ExecuteNonQueryAsync();

                // Delete payroll header rows after detail rows.
                using var headerCommand = connection.CreateCommand();

                headerCommand.Transaction = transaction;

                headerCommand.CommandText = @"
DELETE t
FROM dbo.paytrn t
WHERE TRY_CONVERT(INT, t.[year]) = @Year
  AND TRY_CONVERT(INT, t.[month]) = @Month
  AND UPPER(LTRIM(RTRIM(ISNULL(t.POST_FLAG, '')))) <> 'Y'
  AND (@BranchCode IS NULL OR TRY_CONVERT(INT, t.brnc) = @BranchCode)
  AND
  (
        @EmployeeType = 0
        OR (@EmployeeType = 1 AND TRY_CONVERT(INT, t.[type]) IN (1,2,3,4,5))
        OR (@EmployeeType = 6 AND TRY_CONVERT(INT, t.[type]) = 6)
        OR (@EmployeeType = 7 AND TRY_CONVERT(INT, t.[type]) = 7)
        OR (@EmployeeType = 8 AND TRY_CONVERT(INT, t.[type]) = 8)
        OR (@EmployeeType = 9 AND TRY_CONVERT(INT, t.[type]) = 9)
        OR (@EmployeeType = 10 AND TRY_CONVERT(INT, t.[type]) = 10)
  );";

                AddParameter(headerCommand, "@Year", year);
                AddParameter(headerCommand, "@Month", month);
                AddParameter(headerCommand, "@EmployeeType", employeeType);
                AddParameter(headerCommand, "@BranchCode", branchCode);

                int deletedHeaders = await headerCommand.ExecuteNonQueryAsync();

                await transaction.CommitAsync();

                string scope = branchCode.HasValue
                    ? "Selected Branch"
                    : "All Branches";

                return Json(new
                {
                    success = true,
                    deletedHeaders,
                    deletedDetails,
                    protectedPosted,
                    message = $"{scope}: {deletedHeaders} payroll records and " +
                              $"{deletedDetails} detail records deleted. " +
                              $"{protectedPosted} Final Posted records (POST_FLAG = Y) were kept safe."
                });
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    try
                    {
                        await transaction.RollbackAsync();
                    }
                    catch
                    {
                        // Preserve the original exception.
                    }
                }

                return Json(new
                {
                    success = false,
                    message = "Delete failed. No changes were committed. " + ex.Message
                });
            }
            finally
            {
                if (transaction != null)
                    await transaction.DisposeAsync();

                if (closeConnection && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }

        private static bool IsValidFilter(
            int year,
            int month,
            int employeeType)
        {
            return year >= 1900 &&
                   year <= 9999 &&
                   month >= 1 &&
                   month <= 12 &&
                   (
                       employeeType == 0 ||
                       employeeType == 1 ||
                       employeeType == 6 ||
                       employeeType == 7 ||
                       employeeType == 8 ||
                       employeeType == 9 ||
                       employeeType == 10
                   );
        }

        private bool IsLoggedIn()
        {
            return !string.IsNullOrWhiteSpace(
                HttpContext.Session.GetString("LoginId"));
        }

        private static void AddParameter(
            DbCommand command,
            string name,
            object? value)
        {
            var parameter = command.CreateParameter();

            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;

            command.Parameters.Add(parameter);
        }

        private static double Number(object value)
        {
            return value == DBNull.Value
                ? 0
                : Convert.ToDouble(value);
        }
    }
}
