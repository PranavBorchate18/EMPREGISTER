namespace Employee_Management_System.Models
{
    public class GradeWiseAllowDeductionViewModel
    {
        public List<AllowanceMaster> Allowances { get; set; }
            = new List<AllowanceMaster>();

        public List<DeductionMaster> Deductions { get; set; }
            = new List<DeductionMaster>();

        public List<AllwFoot> AllowanceDetails { get; set; }
            = new List<AllwFoot>();

        public int? SelectedAllowanceCode { get; set; }

        public string? SelectedAllowanceName { get; set; }


        // ==============================
        // DEDUCTION
        // ==============================

        public List<DeduFoot> DeductionDetails { get; set; }
            = new List<DeduFoot>();

        public int? SelectedDeductionCode { get; set; }

        public string? SelectedDeductionName { get; set; }
    }
}