using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employee_Management_System.Models
{
    [Table("Section")] // Change to your actual table name if different
    public class Section
    {
        [Key]
        [Column("code")]
        public int Code { get; set; }

        [Column("name")]
        [StringLength(100)]
        public string? SectionName { get; set; }

        [Column("mname")]
        [StringLength(100)]
        public string? MarathiName { get; set; }

        [Column("New_Code")]
        [StringLength(50)]
        public string? NewCode { get; set; }
    }
}