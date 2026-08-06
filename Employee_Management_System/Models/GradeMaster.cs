using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employee_Management_System.Models
{
    [Table("gradmast")]
    public class GradeMaster
    {
        [Key]
        [Column("code")]
        public int Code { get; set; }

        [Column("name")]
        public string? GradeName { get; set; }

        [Column("New_Code")]
        public decimal? NewCode { get; set; }

        [Column("mname")]
        public string? MarathiName { get; set; }

        // Navigation Property
        public virtual ICollection<Employee>? Employees { get; set; }
    }
}