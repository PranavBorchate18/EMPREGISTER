using System.ComponentModel.DataAnnotations.Schema;

namespace Employee_Management_System.Models
{
    [Table("GradeFoot")]
    public class GradeFoot
    {
        [Column("grade")]
        public int Grade { get; set; }

        [Column("stage")]
        public int Stage { get; set; }

        [Column("basic")]
        public int Basic { get; set; }
    }
}