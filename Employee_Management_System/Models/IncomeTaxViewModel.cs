
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Employee_Management_System.Models
{
    // One row = one month of the financial year
    public class IncomeTaxMonthRow
    {
        public int Year { get; set; }
        public int Month { get; set; }

        public decimal Basic { get; set; }
        public decimal HRA { get; set; }
        public decimal OtherAllowance { get; set; }
        public decimal Prize { get; set; }
        public decimal Bonus { get; set; }
        public decimal RecoveryAllowance { get; set; }
        public decimal GoldAllowance { get; set; }

        // HRA + Other + Prize + Bonus + Recovery + Gold
        public decimal TotalAllowance =>
            HRA + OtherAllowance + Prize + Bonus +
            RecoveryAllowance + GoldAllowance;

        // Backward-compatible property for views/controllers
        public decimal AnnualAllowance => TotalAllowance;

        public decimal Total => Basic + TotalAllowance;

        public string MonthName =>
            new DateTime(Year, Month, 1).ToString("MMM yyyy");
    }

    public class IncomeTaxViewModel
    {
        public int EmployeeCode { get; set; }

        public string EmployeeName { get; set; } = "";
        public string PAN { get; set; } = "";
        public string UID { get; set; } = "";
        public string BranchName { get; set; } = "";

        // Financial year start, e.g. 2026 = FY 2026-27
        public int FinancialYear { get; set; } = DateTime.Today.Month >= 4
            ? DateTime.Today.Year
            : DateTime.Today.Year - 1;

        public List<SelectListItem> Employees { get; set; } = new();

        // April .. March, one row per month
        public List<IncomeTaxMonthRow> Months { get; set; } = new();

        public decimal TotalBasic => Months.Sum(m => m.Basic);
        public decimal TotalAllowanceSum => Months.Sum(m => m.TotalAllowance);

        // Backward-compatible annual allowance property
        public decimal AnnualAllowance => TotalAllowanceSum;

        public decimal TotalHRA => Months.Sum(m => m.HRA);
        public decimal TotalOther => Months.Sum(m => m.OtherAllowance);
        public decimal TotalPrize => Months.Sum(m => m.Prize);
        public decimal TotalBonus => Months.Sum(m => m.Bonus);
        public decimal TotalRecovery => Months.Sum(m => m.RecoveryAllowance);
        public decimal TotalGold => Months.Sum(m => m.GoldAllowance);
        public decimal GrandTotal => Months.Sum(m => m.Total);

        public bool SalaryDataAvailable { get; set; }

        // false when dbo.Incentive_Trn does not exist in this database
        public bool IncentiveAvailable { get; set; } = true;

        // Form 16 employer and certificate details
        public string EmployerName { get; set; } = "";
        public string EmployerAddress { get; set; } = "";
        public string EmployerPAN { get; set; } = "";
        public string EmployerTAN { get; set; } = "";
        public string EmployerCategory { get; set; } = "";

        // Tax deduction and salary computation details
        public decimal TotalTDS { get; set; }
        public decimal GrossSalary { get; set; }
        public decimal Exemptions { get; set; }
        public decimal StandardDeduction { get; set; }
        public decimal ChapterVIA_Deductions { get; set; }
        public decimal TaxableIncome { get; set; }
        public decimal IncomeTax { get; set; }
        public decimal Surcharge { get; set; }
        public decimal Cess { get; set; }
        public decimal TotalTaxPayable { get; set; }
        public decimal TaxRelief { get; set; }
        public decimal NetTaxPayable { get; set; }

        public string AssessmentYear =>
            $"{FinancialYear + 1}-{(FinancialYear + 2) % 100:00}";

        public string Notice { get; set; } =
            "This is a month-wise summary of salary and allowances based on the " +
            "available records. This is not an official Form 16. Verify TDS, " +
            "taxable income, exemptions and tax computation before issuing the " +
            "official certificate.";
    }
}
