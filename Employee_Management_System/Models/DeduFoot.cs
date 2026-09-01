using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employee_Management_System.Models
{
    [Keyless]
    [Table("dedufoot")]
    public class DeduFoot
    {
        [Column("dedu_code")]
        public int DeduCode { get; set; }

        [Column("grad_code")]
        public int GradeCode { get; set; }

        [Column("FIX_amount")]
        public decimal? FixAmount { get; set; }

        [Column("calc_code")]
        public short? CalcCode { get; set; }

        [Column("percent")]
        public decimal? Percent { get; set; }

        [Column("min_limit")]
        public decimal? MinLimit { get; set; }

        [Column("max_limit")]
        public decimal? MaxLimit { get; set; }

        [Column("min_days")]
        public int? MinDays { get; set; }
    }
}