using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employee_Management_System.Models
{
    [Table("ReligionMast")]
    public class ReligionMaster
    {
        [Key]
        [Column("Code")]
        public decimal Code { get; set; }

        [Column("Name")]
        public string ReligionName { get; set; } = string.Empty;

        [Column("MName")]
        public string? MarathiName { get; set; }
    }
}