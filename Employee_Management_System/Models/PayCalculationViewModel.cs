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
        public int EmployeeType { get; set; }
        public bool IsPermanent => EmployeeType >= 1 && EmployeeType <= 5;
        public double OriginalBasicSalary { get; set; }
        public double WithoutPayLeaveDays { get; set; }
        public double WithoutPayLeaveAmount { get; set; }
        public double BasicSalary { get; set; }
        public Dictionary<string, double> Allowances { get; set; } = new();
        public double Fda1 { get; set; }
        public double Fda2 { get; set; }
        public double Vda { get; set; }
        public double SubTotal { get; set; }
        public double Hra { get; set; }
        public double CashAllowance { get; set; }
        public double SroAllowance { get; set; }
        public double SpecialPay { get; set; }
        public double TotalAllowance { get; set; }
        public double GrossSalary { get; set; }
        public double TotalSalary => GrossSalary;
        public Dictionary<string, double> Deductions { get; set; } = new();
        public double ProvidentFund { get; set; }
        public double TotalDeduction { get; set; }
        public double NetSalary { get; set; }
    }
}
