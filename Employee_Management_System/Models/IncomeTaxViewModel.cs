
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Employee_Management_System.Models
{
    public class IncomeTaxViewModel
    {
        public int EmployeeCode { get; set; }

        public string EmployeeName { get; set; } = "";
        public string PAN { get; set; } = "";

        // Financial year start, e.g. 2026 = FY 2026-27
        public int FinancialYear { get; set; } = DateTime.Today.Month >= 4
            ? DateTime.Today.Year
            : DateTime.Today.Year - 1;

        public List<SelectListItem> Employees { get; set; } = new();

        public decimal AnnualBasic { get; set; }
        public decimal AnnualAllowance { get; set; }
        public decimal AnnualGrossSalary { get; set; }

        // Do not populate this from total deductions.
        // A verified TDS source must be configured first.
        public decimal? VerifiedTds { get; set; }

        public bool SalaryDataAvailable { get; set; }

        public string Notice { get; set; } =
            "Draft salary summary only. Verify tax details before issuing Form 16.";
    }
}
