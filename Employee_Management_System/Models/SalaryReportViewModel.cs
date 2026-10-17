using System;
using System.Collections.Generic;
namespace Employee_Management_System.Models
{
    public class SalaryReportViewModel
    {
        public int SelectedEmployeeType { get; set; } = 0;
        public int FromYear { get; set; }
        public int FromMonth { get; set; }
        public int ToYear { get; set; }
        public int ToMonth { get; set; }
        public List<SalaryReportBranchViewModel> Branches { get; set; } = new();
        public int AllBranchesTotalEmployees { get; set; }
        public double AllBranchesTotalBasic { get; set; }
        public double AllBranchesTotalAllowance { get; set; }
        public double AllBranchesTotalDeduction { get; set; }
        public double AllBranchesTotalNetSalary { get; set; }
        public List<int> SelectedAllowanceCodes { get; set; } = new();
        public List<int> SelectedDeductionCodes { get; set; } = new();
        public List<SalaryReportComponentViewModel> AllowanceOptions { get; set; } = new();
        public List<SalaryReportComponentViewModel> DeductionOptions { get; set; } = new();
    }
    public class SalaryReportComponentViewModel
    {
        public const int BasicCode = -1;
        public int Code { get; set; }
        public string Name { get; set; } = "";
    }
    public class SalaryReportBranchViewModel
    {
        public int BranchCode { get; set; }
        public string BranchName { get; set; } = "";
        public int EmployeeCount { get; set; }
        public double TotalBasic { get; set; }
        public double TotalAllowance { get; set; }
        public double TotalDeduction { get; set; }
        public double TotalNetSalary { get; set; }
    }
    public class SalaryReportEmployeeRowViewModel
    {
        public int EmployeeCode { get; set; }
        public string EmployeeName { get; set; } = "";
        public int? GradeCode { get; set; }
        public string GradeName { get; set; } = "";
        public double BasicSalary { get; set; }
        public double OriginalBasicSalary { get; set; }
        public double WithoutPayLeaveDays { get; set; }
        public double WithoutPayLeaveAmount => WithoutPayLeaveDays > 0
            ? Math.Round(Math.Max(0, OriginalBasicSalary - BasicSalary), 2) : 0;
        public Dictionary<string, double> Allowances { get; set; } = new();
        public double TotalAllowance { get; set; }
        public double GrossSalary { get; set; }
        public Dictionary<string, double> Deductions { get; set; } = new();
        public double TotalDeduction { get; set; }
        public double NetSalary { get; set; }
        public List<SalaryReportMonthHistoryViewModel> History { get; set; } = new();
    }
    public class SalaryReportMonthHistoryViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = "";
        public double BasicSalary { get; set; }
        public double OriginalBasicSalary { get; set; }
        public double WithoutPayLeaveDays { get; set; }
        public double WithoutPayLeaveAmount => WithoutPayLeaveDays > 0
            ? Math.Round(Math.Max(0, OriginalBasicSalary - BasicSalary), 2) : 0;
        public Dictionary<string, double> Allowances { get; set; } = new();
        public double TotalAllowance { get; set; }
        public double GrossSalary { get; set; }
        public Dictionary<string, double> Deductions { get; set; } = new();
        public double TotalDeduction { get; set; }
        public double NetSalary { get; set; }
    }
}
