using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using static System.Collections.Specialized.BitVector32;

namespace Employee_Management_System.Models
{
    public class Employee
    {
        public int Id { get; set; }

        [Required]
        public string EmployeeCode { get; set; }

        [Required]
        public string EmployeeName { get; set; }

        public string EmployeeType { get; set; }

        // General Information

        public int? GradeId { get; set; }

        public int? SectionId { get; set; }

        public int? BranchId { get; set; }

        public DateTime? JoiningDate { get; set; }

        public DateTime? PermanentDate { get; set; }

        public DateTime? LastSalaryDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BasicSalary { get; set; }

        public DateTime? LastSalaryIncrementDate { get; set; }

        public DateTime? RetirementDate { get; set; }

        // Navigation Properties

        public GradeFoot Grade { get; set; }

        public Section Section { get; set; }

        public Branch Branch { get; set; }

        // ===============================
        // Other Information
        // ===============================

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PensionFundOpeningBalance { get; set; }

        public string? PFNumber { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PFOpeningBalance { get; set; }

        public string? PANNumber { get; set; }

        public string? SavingGLCode { get; set; }

        public string? SavingBranchCode { get; set; }

        public string? SavingAccountCode { get; set; }

        public string? ITSerialNumber { get; set; }

        public string? PFSerialNumber { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PFBalance { get; set; }

        public string? AadhaarNumber { get; set; }

        //================ Address Information =================

        [Display(Name = "Correspondence Address")]
        public string? CorrespondenceAddress1 { get; set; }

        public string? CorrespondenceAddress2 { get; set; }

        public string? PermanentAddress1 { get; set; }

        public string? PermanentAddress2 { get; set; }

        public string? FatherName { get; set; }

        public string? Address1 { get; set; }

        public string? Address2 { get; set; }

        //====================== Personal Information ======================

        public string? Gender { get; set; }

        public DateTime? BirthDate { get; set; }

        public string? BloodGroup { get; set; }

        public string? IdentificationMark { get; set; }

        public decimal? Height { get; set; }

        public string? KnownLanguage { get; set; }

        public string? MotherTongue { get; set; }

        public string? Education { get; set; }

        public string? ModeOfSign { get; set; }

        public string? Religion { get; set; }

        public string? Caste { get; set; }
    }
}