using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employee_Management_System.Models
{
    [Table("allwmast")]
    public class AllowanceMaster
    {
        [Key]
        [Column("code")]
        [Display(Name = "Allowance Code")]
        public int Code { get; set; }


        [Column("name")]
        [StringLength(30)]
        [Display(Name = "Allowance Name")]
        public string? Name { get; set; }


        [Column("short_name")]
        [StringLength(15)]
        [Display(Name = "Short Name")]
        public string? ShortName { get; set; }


        [Column("glc")]
        [Display(Name = "G/L Code")]
        public int? GLC { get; set; }


        [Column("comp")]
        [StringLength(1)]
        [Display(Name = "Comp")]
        public string? Comp { get; set; }


        [Column("effect_on_pay")]
        [StringLength(1)]
        [Display(Name = "Effect On Pay")]
        public string? EffectOnPay { get; set; }


        [Column("Effect_On_Trf")]
        [StringLength(1)]
        [Display(Name = "Effect On Trf")]
        public string? EffectOnTrf { get; set; }


        [Column("Trf_Min_Days")]
        [Display(Name = "Trf Min Days")]
        public decimal? TrfMinDays { get; set; }
    }
}