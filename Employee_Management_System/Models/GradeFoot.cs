using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employee_Management_System.Models
{
    [Table("Grade")]   // Change "Grade" to your actual table name if different
    public class GradeFoot
    {
        [Key]
        [Column("code")]
        public int Code { get; set; }

        [Column("name")]
        [StringLength(100)]
        public string? GradeName { get; set; }

        [Column("mname")]
        [StringLength(100)]
        public string? MarathiName { get; set; }

        [Column("New_Code")]
        [StringLength(50)]
        public string? NewCode { get; set; }

        [Column("prmn-basic")]
        public decimal? PermanentBasic { get; set; }

        [Column("temp-basic")]
        public decimal? TemporaryBasic { get; set; }

        [Column("daily-basic")]
        public decimal? DailyBasic { get; set; }
    }
}