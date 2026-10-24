using System;
using System.Collections.Generic;

namespace Employee_Management_System.Models
{
    public class EmployeeDashboardViewModel
    {
        public DateTime? WorkingDate { get; set; }
        public int ActiveEmployees { get; set; }
        public int MaleEmployees { get; set; }
        public int FemaleEmployees { get; set; }
        public int TodayPresentEmployees { get; set; }
        public int TodayAbsentEmployees { get; set; }

        // Main Dashboard filter
        public int? SelectedDivision { get; set; }
        public int? SelectedGrade { get; set; }
        public string SelectedGender { get; set; } = "";

        public int SelectedSalaryYear { get; set; }
        public int SelectedSalaryMonth { get; set; }

        // Bottom Gross Salary filter remains Branch-based
        public int? SelectedGrossSalaryBranch { get; set; }
        public DateTime GrossSalaryFromDate { get; set; }
        public DateTime GrossSalaryToDate { get; set; }
        public double TotalGrossSalary { get; set; }

        public List<EmployeeDashboardOptionViewModel> Divisions { get; set; } = new();
        public List<EmployeeDashboardOptionViewModel> Branches { get; set; } = new();
        public List<EmployeeDashboardOptionViewModel> Grades { get; set; } = new();

        public List<BranchEmployeeChartViewModel> BranchWiseEmployees { get; set; } = new();
        public List<BranchSalaryChartViewModel> BranchWiseSalaries { get; set; } = new();
        public List<MonthlyGrossSalaryChartViewModel> MonthlyGrossSalaries { get; set; } = new();

        // Payroll rows awaiting final save (dbo.paytrn.POST_FLAG = 'N').
        public List<PendingFinalPayListViewModel> PendingFinalPayLists { get; set; } = new();
    }

    public class PendingFinalPayListViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int BranchCode { get; set; }
        public string BranchName { get; set; } = "";
        public int TotalEmployees { get; set; }
        public double TotalBasic { get; set; }
        public double TotalAllowance { get; set; }
        public double TotalDeduction { get; set; }
        public double NetSalary { get; set; }
    }

    public class EmployeeDashboardOptionViewModel
    {
        public int Value { get; set; }
        public string Text { get; set; } = "";
    }

    public class BranchEmployeeChartViewModel
    {
        public int BranchCode { get; set; }
        public string BranchName { get; set; } = "";
        public int EmployeeCount { get; set; }
    }

    public class BranchSalaryChartViewModel
    {
        public int BranchCode { get; set; }
        public string BranchName { get; set; } = "";
        public double Salary { get; set; }
    }

    public class MonthlyGrossSalaryChartViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = "";
        public double GrossSalary { get; set; }
    }
}
