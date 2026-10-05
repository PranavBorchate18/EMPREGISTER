using System.Collections.Generic;

namespace Employee_Management_System.Models
{
    public class PayCalculationViewModel
    {
        public int SelectedYear { get; set; }
        public int SelectedMonth { get; set; }
        public double ProfessionalTax { get; set; }
        public List<PayCalculationBranchViewModel> Branches { get; set; } = new();
    }

    public class PayCalculationBranchViewModel
    {
        public int BranchCode { get; set; }
        public string BranchName { get; set; } = "";
        public int EmployeeCount { get; set; }
    }

    public class EmployeeSalaryRowViewModel
    {
        public int EmployeeCode { get; set; }
        public string EmployeeName { get; set; } = "";
        public int? GradeCode { get; set; }
        public string GradeName { get; set; } = "";

        // Basic before Without Pay Leave reduction (PayMast.basic).
        public double OriginalBasicSalary { get; set; }

        // Approved WPL days for the selected month.
        public double WithoutPayLeaveDays { get; set; }

        // Amount reduced from Basic because of WPL.
        public double WithoutPayLeaveAmount { get; set; }

        // Payable Basic after WPL reduction.
        public double BasicSalary { get; set; }

        public Dictionary<string, double> Allowances { get; set; } = new();
        public double TotalAllowance { get; set; }
        public double GrossSalary { get; set; }
        public Dictionary<string, double> Deductions { get; set; } = new();
        public double TotalDeduction { get; set; }
        public double NetSalary { get; set; }
    }
}
