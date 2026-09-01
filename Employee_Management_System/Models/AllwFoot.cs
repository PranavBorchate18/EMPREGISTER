using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employee_Management_System.Models
{
    [Keyless]
    [Table("allwfoot")]
    public class AllwFoot
    {
        [Column("allw_code")]
        public decimal AllowCode { get; set; }

        [Column("grad_code")]
        public decimal GradeCode { get; set; }

        [Column("Pay_Div")]
        public decimal PayDiv { get; set; }

        [Column("Fix_Amount")]
        public decimal? FixAmount { get; set; }

        [Column("calc_code")]
        public decimal? CalcCode { get; set; }

        [Column("percent")]
        public decimal? Percent { get; set; }

        [Column("min_limit")]
        public decimal? MinLimit { get; set; }

        [Column("max_limit")]
        public decimal? MaxLimit { get; set; }

        [Column("min_days")]
        public decimal? MinDays { get; set; }
    }
}