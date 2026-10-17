using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Employee_Management_System.Models
{
    public class EmployeePaySetupViewModel
    {
        public int? EmployeeCode { get; set; }
        public int? EmployeeType { get; set; } // dbo.paymast.type: 6 = Contract
        public string? EmployeeName { get; set; }
        public double? BasicSalary { get; set; }
        public int? GradeCode { get; set; }
        public int? Flag { get; set; }
        public int? ComponentCode { get; set; }
        public string? ComponentName { get; set; }
        public int? CalculationCode { get; set; }
        public double? Percentage { get; set; }
        public double? Amount { get; set; }
        public double? MinLimit { get; set; }
        public double? MaxLimit { get; set; }
        public double? MinDays { get; set; }
        public double? PayDiv { get; set; }
        public bool? Apply { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public DateTime? CancelDate { get; set; }
        public int? ForTimes { get; set; }
        public int? GLC { get; set; }
        public int? Branch { get; set; }
        public int? SLC { get; set; }
        public double? LoanAdvanceAmount { get; set; }
        public List<SelectListItem> Components { get; set; } = new();
        public List<EmployeePayComponentRow> ExistingComponents { get; set; } = new();
    }

    public class EmployeePayComponentRow
    {
        public int? EmployeeCode { get; set; }
        public int? Flag { get; set; }
        public string? TypeName { get; set; }
        public int? ComponentCode { get; set; }
        public string? ComponentName { get; set; }
        public int? CalculationCode { get; set; }
        public double? Percentage { get; set; }
        public double? Amount { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public DateTime? CancelDate { get; set; }
        public int? ForTimes { get; set; }
        public int? GLC { get; set; }
        public int? Branch { get; set; }
        public int? SLC { get; set; }
        public double? LoanAdvanceAmount { get; set; }
    }
}
