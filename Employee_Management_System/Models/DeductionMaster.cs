using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employee_Management_System.Models
{
    [Table("dedumast")]
    public class DeductionMaster
    {
        [Key]
        [Column("code")]
        public int Code { get; set; }


        [Column("name")]
        [StringLength(30)]
        public string? Name { get; set; }


        [Column("comp")]
        [StringLength(1)]
        public string? Comp { get; set; }


        [Column("short_name")]
        [StringLength(15)]
        public string? ShortName { get; set; }


        [Column("glc")]
        public int? GLC { get; set; }


        [Column("flag")]
        [StringLength(1)]
        public string? Flag { get; set; }


        [Column("HO_CUTTING")]
        public bool? HOCutting { get; set; }


        [Column("PrintIn_IncomeTax_Report")]
        public bool? PrintInIncomeTaxReport { get; set; }


        // =====================================================
        // ACTIVE
        // Y = Yes
        // N = No
        // =====================================================

        [Column("Active")]
        [StringLength(1)]
        public string? Active { get; set; }


        // =====================================================
        // APPLY FOR MONTH
        //
        // January  = 1
        // February = 2
        // ...
        // December = 12
        //
        // Example multiple:
        // 1,4,7
        // =====================================================

        [Column("Apply_Month")]
        [StringLength(50)]
        public string? ApplyMonth { get; set; }
    }
}