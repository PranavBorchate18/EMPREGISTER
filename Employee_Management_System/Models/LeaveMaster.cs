using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employee_Management_System.Models
{
    [Table("leavmast")]
    public class LeaveMaster
    {
        [Key]
        [Column("code")]
        public short Code { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("short_name")]
        public string? ShortName { get; set; }

        [Column("prmn_year")]
        public decimal? PermanentYear { get; set; }

        [Column("prmn_month")]
        public decimal? PermanentMonth { get; set; }

        [Column("temp_month")]
        public decimal? TemporaryMonth { get; set; }

        [Column("store_yn")]
        public string? StoreYN { get; set; }

        [Column("store_limit")]
        public decimal? StoreLimit { get; set; }

        [Column("cash_limit")]
        public decimal? CashLimit { get; set; }

        [Column("flag")]
        public string? Flag { get; set; }

        [Column("leave_limit")]
        public decimal? LeaveLimit { get; set; }

        [Column("min_limit")]
        public decimal? MinimumLimit { get; set; }

        // Y = Active, N = Deactive
        [Column("Active")]
        public string? Active { get; set; }
    }
}
