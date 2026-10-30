using System.Collections.Generic;

namespace Employee_Management_System.Models
{
    public class GradeWiseAllowDeductionViewModel
    {
        public List<AllowanceMaster> Allowances { get; set; } = new();
        public List<DeductionMaster> Deductions { get; set; } = new();
        public List<GradeMaster> Grades { get; set; } = new();

        public List<GradeWiseAllowanceDetailViewModel> AllowanceDetails { get; set; } = new();
        public int? SelectedAllowanceCode { get; set; }
        public string? SelectedAllowanceName { get; set; }
        public Dictionary<int, int> AllowanceSelectedGradeCounts { get; set; } = new();

        public List<GradeWiseDeductionDetailViewModel> DeductionDetails { get; set; } = new();
        public int? SelectedDeductionCode { get; set; }
        public string? SelectedDeductionName { get; set; }
        public Dictionary<int, int> DeductionSelectedGradeCounts { get; set; } = new();
    }

    public class GradeWiseAllowanceDetailViewModel
    {
        public int Code { get; set; }
        public string? Name { get; set; }
        public bool IsSelected { get; set; }
        public decimal PayDiv { get; set; }
        public decimal? FixAmount { get; set; }
        public decimal? CalcCode { get; set; }
        public decimal? Percent { get; set; }
        public decimal? MinLimit { get; set; }
        public decimal? MaxLimit { get; set; }
        public decimal? MinDays { get; set; }
        public string? Formula { get; set; }
    }

    public class GradeWiseDeductionDetailViewModel
    {
        public int Code { get; set; }
        public string? Name { get; set; }
        public bool IsSelected { get; set; }
        public decimal? FixAmount { get; set; }
        public short? CalcCode { get; set; }
        public decimal? Percent { get; set; }
        public decimal? MinLimit { get; set; }
        public decimal? MaxLimit { get; set; }
        public int? MinDays { get; set; }
    }
}
