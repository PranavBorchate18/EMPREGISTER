using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employee_Management_System.Models
{
    [Table("brncmast")]
    public class Branch
    {
        [Key]
        [Column("code")]
        public short Code { get; set; }

        [Column("name")]
        public string? BranchName { get; set; }

        public virtual ICollection<Employee>? Employees { get; set; }
    }
}