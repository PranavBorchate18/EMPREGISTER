using System.ComponentModel.DataAnnotations;

namespace Employee_Management_System.Models
{
    public class SalaryRevisionViewModel
    {
        // =========================================================
        // EMPLOYEE
        // =========================================================

        [Required(ErrorMessage = "Employee Code is required.")]
        [Display(Name = "Employee Code")]
        public int? EmployeeCode { get; set; }


        [Display(Name = "Employee Name")]
        public string? EmployeeName { get; set; }


        [Display(Name = "Current Basic")]
        public double CurrentBasic { get; set; }


        [Display(Name = "Last Increment Date")]
        [DataType(DataType.Date)]
        public DateTime? LastIncrementDate { get; set; }


        // =========================================================
        // REVISION
        // =========================================================

        [Required(ErrorMessage = "Please select Revision Type.")]
        [Display(Name = "Revision Type")]
        public string? RevisionType { get; set; }


        [Required(ErrorMessage = "Revision Amount is required.")]
        [Range(
            0.01,
            double.MaxValue,
            ErrorMessage = "Revision Amount must be greater than zero."
        )]
        [Display(Name = "Revision Amount")]
        public double RevisionAmount { get; set; }


        [Display(Name = "New Basic")]
        public double NewBasic { get; set; }


        // =========================================================
        // DATE
        // =========================================================

        [Required(ErrorMessage = "Effective From Date is required.")]
        [Display(Name = "Effective From Date")]
        [DataType(DataType.Date)]
        public DateTime FromDate { get; set; }


        [Required(ErrorMessage = "Effective To Date is required.")]
        [Display(Name = "Effective To Date")]
        [DataType(DataType.Date)]
        public DateTime ToDate { get; set; }
    }
}