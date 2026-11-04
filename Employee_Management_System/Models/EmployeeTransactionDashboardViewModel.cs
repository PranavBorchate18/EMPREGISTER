
using System;
using System.Collections.Generic;

namespace Employee_Management_System.Models
{
    public class EmployeeTransactionDashboardViewModel
    {
        // Employee Search
        public int? EmployeeCode { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        // Employee Information
        public bool EmployeeFound { get; set; }
        public string EmployeeName { get; set; } = "";
        public int? EmployeeType { get; set; }
        public string GradeName { get; set; } = "";
        public string BranchName { get; set; } = "";
        public DateTime? JoiningDate { get; set; }
        public double? BasicSalary { get; set; }

        // Error Message
        public string ErrorMessage { get; set; } = "";

        // 1. Employee Pay Setup
        public List<Dictionary<string, string>> PaySetup { get; set; } = new();

        // 2. Salary History - Date Range Summary
        public List<Dictionary<string, string>> MonthlySalary { get; set; } = new();

        // 3. Leave Credit / Debit / Leave Balance
        public List<Dictionary<string, string>> LeaveCreditDebit { get; set; } = new();

        // 4. Employee Type Change History
        public List<Dictionary<string, string>> EmployeeTypeHistory { get; set; } = new();
    }
}
