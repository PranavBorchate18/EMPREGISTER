using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employee_Management_System.Models
{
    [Table("PayMast")]
    public class PayMast
    {
        [Key]
        [Column("code")]
        public int EmployeeCode { get; set; }

        [Column("name_Prefix")]
        public string? NamePrefix { get; set; }

        [Column("name")]
        public string? EmployeeName { get; set; }

        [Column("ename")]
        public string? EnglishName { get; set; }

        [Column("type")]
        public int? EmployeeType { get; set; }

        [Column("join_date")]
        public DateTime? JoiningDate { get; set; }

        [Column("Re_Join_Date")]
        public DateTime? ReJoinDate { get; set; }

        [Column("prmn_date")]
        public DateTime? PermanentDate { get; set; }

        [Column("Resign_Date")]
        public DateTime? ResignDate { get; set; }

        [Column("grad_Code")]
        public int? GradeId { get; set; }

        [Column("brnc_code")]
        public int? BranchId { get; set; }

        [Column("depo_code")]
        public string? DepartmentCode { get; set; }

        [Column("given_date")]
        public DateTime? GivenDate { get; set; }

        [Column("basic")]
        public double? BasicSalary { get; set; }

        [Column("ppf_opnbal")]
        public double? PensionFundOpeningBalance { get; set; }

        [Column("pf_no")]
        public string? PFNo { get; set; }

        [Column("pf_srno")]
        public int? PFSrNo { get; set; }

        [Column("it_srno")]
        public string? ITSrNo { get; set; }

        [Column("section")]
        public int? SectionId { get; set; }

        [Column("last_incr_date")]
        public DateTime? LastIncrementDate { get; set; }

        [Column("cast")]
        public int? CasteId { get; set; }

        [Column("sub_cast")]
        public int? SubCasteId { get; set; }

        [Column("corr_addr1")]
        public string? CorrespondenceAddress1 { get; set; }

        [Column("corr_addr2")]
        public string? CorrespondenceAddress2 { get; set; }

        [Column("corr_addr3")]
        public string? CorrespondenceAddress3 { get; set; }

        [Column("prmn_addr1")]
        public string? PermanentAddress1 { get; set; }

        [Column("prmn_addr2")]
        public string? PermanentAddress2 { get; set; }

        [Column("prmn_addr3")]
        public string? PermanentAddress3 { get; set; }

        [Column("sex")]
        public string? Sex { get; set; }

        [Column("father_name")]
        public string? FatherName { get; set; }

        [Column("father_addr1")]
        public string? FatherAddress1 { get; set; }

        [Column("father_addr2")]
        public string? FatherAddress2 { get; set; }

        [Column("birth_date")]
        public DateTime? BirthDate { get; set; }

        [Column("id_mark")]
        public string? IdentificationMark { get; set; }

        [Column("bl_group")]
        public string? BloodGroup { get; set; }

        [Column("lang_known")]
        public string? LanguagesKnown { get; set; }

        [Column("mother_tongue")]
        public string? MotherTongue { get; set; }

        [Column("qualification")]
        public string? Qualification { get; set; }

        [Column("modeofsign")]
        public string? ModeOfSign { get; set; }

        [Column("incr_basic")]
        public double? IncrementBasic { get; set; }

        [Column("pf_balance")]
        public int? PFBalance { get; set; }

        [Column("pf_opbal")]
        public double? PFOpeningBalance { get; set; }

        [Column("calpt")]
        public string? CalculationPoint { get; set; }

        [Column("Retire_Date")]
        public DateTime? RetirementDate { get; set; }

        [Column("Mobile_No1")]
        public string? MobileNo1 { get; set; }

        [Column("Mobile_No2")]
        public string? MobileNo2 { get; set; }

        [Column("Phone1")]
        public string? Phone1 { get; set; }

        [Column("Phone2")]
        public string? Phone2 { get; set; }

        [Column("Pan_No")]
        public string? PANNo { get; set; }

        [Column("Mother_Name")]
        public string? MotherName { get; set; }

        [Column("Wife_Name")]
        public string? WifeName { get; set; }

        [Column("Son_Name1")]
        public string? SonName1 { get; set; }

        [Column("Son_Name2")]
        public string? SonName2 { get; set; }

        [Column("Son_Name3")]
        public string? SonName3 { get; set; }

        [Column("Dot_Name1")]
        public string? DaughterName1 { get; set; }

        [Column("Dot_Name2")]
        public string? DaughterName2 { get; set; }

        [Column("Dot_Name3")]
        public string? DaughterName3 { get; set; }

        [Column("hight")]
        public decimal? Height { get; set; }

        [Column("uan_no")]
        public string? UANNo { get; set; }

        [Column("Leav_St_Date")]
        public DateTime? LeaveStartDate { get; set; }

        [Column("Marital_Status")]
        public decimal? MaritalStatus { get; set; }

        [Column("Taluka")]
        public decimal? Taluka { get; set; }

        [Column("District")]
        public decimal? District { get; set; }

        [Column("Bank_Name")]
        public string? BankName { get; set; }

        [Column("Bank_Acno")]
        public string? BankAccountNo { get; set; }

        [Column("Bank_Ifsc")]
        public string? BankIFSC { get; set; }

        [Column("Aadhar_No")]
        public decimal? AadharNo { get; set; }

        [Column("Secu_Depo")]
        public decimal? SecurityDeposit { get; set; }

        [Column("Party_code")]
        public decimal? CustomerId { get; set; }

        [Column("Entry_Date")]
        public DateTime? EntryDate { get; set; }

        [Column("Opn_By")]
        public string? OpenedBy { get; set; }

        [Column("Opn_IP")]
        public string? OpenedIP { get; set; }

        [Column("Insu_Policy_No")]
        public string? InsurancePolicyNo { get; set; }

        [Column("Married_YN")]
        public string? MarriedYN { get; set; }

        [Column("Spouse_Name")]
        public string? SpouseName { get; set; }

        [Column("EMail_ID")]
        public string? EmailId { get; set; }

        [Column("Religion")]
        public decimal? Religion { get; set; }
    }
}