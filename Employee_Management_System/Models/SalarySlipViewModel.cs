using System;
using System.Collections.Generic;

namespace Employee_Management_System.Models
{
    public class SalarySlipViewModel
    {
        // ==========================================
        // SEARCH
        // ==========================================
        public int Year { get; set; }
        public int Month { get; set; }
        public int EmployeeCode { get; set; }

        public bool IsGenerated { get; set; }
        public string Message { get; set; } = "";

        // ==========================================
        // EMPLOYEE DETAILS - dbo.PayMast
        // ==========================================
        public string EmployeeName { get; set; } = "";
        public string Designation { get; set; } = "";
        public string BranchName { get; set; } = "";

        public DateTime? JoiningDate { get; set; }
        public DateTime? RetirementDate { get; set; }
        public DateTime? BirthDate { get; set; }

        public string PFNumber { get; set; } = "";
        public string UANNumber { get; set; } = "";
        public string PANNumber { get; set; } = "";
        public string AadhaarNumber { get; set; } = "";

        // ==========================================
        // SALARY - dbo.paytrn
        // ==========================================
        public double BasicSalary { get; set; }
        public double DA { get; set; }
        public double HRA { get; set; }

        public double TotalAllowance { get; set; }
        public double GrossSalary { get; set; }

        public double TotalDeduction { get; set; }
        public double NetSalary { get; set; }

        public double PayableDays { get; set; }

        // ==========================================
        // DETAILS - dbo.paytrnft
        // ==========================================
        public List<SalarySlipComponentViewModel> Earnings { get; set; } = new();

        public List<SalarySlipComponentViewModel> Deductions { get; set; } = new();

        // ==========================================
        // DISPLAY
        // ==========================================
        public string MonthName { get; set; } = "";
        public string NetSalaryInWords { get; set; } = "";
    }

    public class SalarySlipComponentViewModel
    {
        public int Code { get; set; }
        public string Name { get; set; } = "";
        public double Amount { get; set; }
    }
}