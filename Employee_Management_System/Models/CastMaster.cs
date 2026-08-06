using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employee_Management_System.Models
{
    [Table("CastMast")]
    public class CastMaster
    {
        [Key]
        [Column("Code")]
        public decimal Code { get; set; }

        [Column("Name")]
        public string? CastName { get; set; }

        [Column("MName")]
        public string? MarathiName { get; set; }
    }
}