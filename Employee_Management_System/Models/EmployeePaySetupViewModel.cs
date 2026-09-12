using Microsoft.AspNetCore.Mvc.Rendering;

namespace Employee_Management_System.Models
{
    public class EmployeePaySetupViewModel
    {
        // =========================================================
        // EMPLOYEE - dbo.PayMast
        // =========================================================

        public int? EmployeeCode { get; set; }

        public string? EmployeeName { get; set; }

        public double? BasicSalary { get; set; }

        public int? GradeCode { get; set; }


        // =========================================================
        // TYPE
        // 1 = Allowance
        // 2 = Deduction
        // =========================================================

        public int? Flag { get; set; }


        // =========================================================
        // COMPONENT
        // allwfoot + allwmast
        // dedufoot + dedumast
        // =========================================================

        public int? ComponentCode { get; set; }

        public string? ComponentName { get; set; }


        // =========================================================
        // CALCULATION RULE
        // allwfoot / dedufoot
        // =========================================================

        public int? CalculationCode { get; set; }

        public double? Percentage { get; set; }

        public double? Amount { get; set; }

        public double? MinLimit { get; set; }

        public double? MaxLimit { get; set; }

        public double? MinDays { get; set; }

        public double? PayDiv { get; set; }

        public bool? Apply { get; set; }


        // =========================================================
        // EMPLOYEE SPECIFIC DETAILS
        // saved in dbo.payfoot
        // =========================================================

        public DateTime? EffectiveDate { get; set; }

        public DateTime? CancelDate { get; set; }

        public int? ForTimes { get; set; }

        public int? GLC { get; set; }

        public int? Branch { get; set; }

        public int? SLC { get; set; }

        public double? LoanAdvanceAmount { get; set; }


        // =========================================================
        // COMPONENT DROPDOWN
        // =========================================================

        public List<SelectListItem> Components { get; set; }
            = new List<SelectListItem>();


        // =========================================================
        // EXISTING dbo.payfoot RECORDS
        // =========================================================

        public List<EmployeePayComponentRow> ExistingComponents { get; set; }
            = new List<EmployeePayComponentRow>();
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