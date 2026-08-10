using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employee_Management_System.Models
{
    [Table("prtymast")]
    public class PartyMaster
    {
        [Key]
        [Column("CODE")]
        public decimal Code { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("ename")]
        public string? EnglishName { get; set; }

        [Column("ADDR1")]
        public string? Address1 { get; set; }

        [Column("ADDR2")]
        public string? Address2 { get; set; }

        [Column("ADDR3")]
        public string? Address3 { get; set; }

        [Column("ADDR4")]
        public string? Address4 { get; set; }

        [Column("ADDR5")]
        public string? Address5 { get; set; }

        [Column("PIN")]
        public string? Pin { get; set; }

        [Column("CorAddr1")]
        public string? CorrespondenceAddress1 { get; set; }

        [Column("CorAddr2")]
        public string? CorrespondenceAddress2 { get; set; }

        [Column("CorAddr3")]
        public string? CorrespondenceAddress3 { get; set; }

        [Column("CorPincCode")]
        public string? CorrespondencePinCode { get; set; }

        [Column("PHONE")]
        public string? Phone { get; set; }

        [Column("PHONE1")]
        public string? Phone1 { get; set; }

        [Column("Mobile")]
        public string? Mobile { get; set; }

        [Column("EMAIL_ID")]
        public string? EmailId { get; set; }

        [Column("SEX")]
        public string? Sex { get; set; }

        [Column("birthdate")]
        public DateTime? BirthDate { get; set; }

        [Column("FATHERNAME")]
        public string? FatherName { get; set; }

        [Column("pan_no")]
        public string? PanNumber { get; set; }

        [Column("AdharNo")]
        public string? AadhaarNumber { get; set; }

        [Column("NATIONALITY")]
        public string? Nationality { get; set; }

        [Column("Religion")]
        public string? Religion { get; set; }

        [Column("Cast")]
        public string? Caste { get; set; }

        [Column("Area")]
        public string? Area { get; set; }

        [Column("City")]
        public string? City { get; set; }

        [Column("Taluka")]
        public string? Taluka { get; set; }

        [Column("District")]
        public string? District { get; set; }

        [Column("State")]
        public string? State { get; set; }

        [Column("VOTERIDNO")]
        public string? VoterIdNumber { get; set; }

        [Column("PASSPORTNO")]
        public string? PassportNumber { get; set; }

        [Column("GST_No")]
        public string? GstNumber { get; set; }

        [Column("Driving_License")]
        public string? DrivingLicense { get; set; }

        [Column("Driving_License_ExpDate")]
        public DateTime? DrivingLicenseExpiryDate { get; set; }
    }
}