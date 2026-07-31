using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employee_Management_System.Models
{
    [Table("Branch")] // Replace with your actual table name if different
    public class Branch
    {
        [Key]
        [Column("code")]          // Database column name
        public int Code { get; set; }

        [Column("name")]          // Database column name
        [StringLength(100)]
        public string? BranchName { get; set; }

        [Column("mname")]         // Database column name
        [StringLength(100)]
        public string? MarathiName { get; set; }

        [Column("New_Code")]      // Database column name
        [StringLength(50)]
        public string? NewCode { get; set; }
    }
}