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


        // =====================================================
        // APPLY MONTH
        // Example:
        // January = 1
        // January + April + July = 1,4,7
        // =====================================================

        [Column("Apply_Month")]
        [StringLength(50)]
        [Display(Name = "Apply for Month")]
        public string? ApplyMonth { get; set; }


        // =====================================================
        // ACTIVE
        // Yes / No
        // =====================================================

        [Column("Active")]
        [StringLength(3)]
        [Display(Name = "Active")]
        public string? Active { get; set; }


        // =====================================================
        // ALLOWANCE TYPE  (dbo.allwmast.Allow_Type)
        // 1 = General
        // 2 = HRA
        // 3 = DA
        // =====================================================

        public const int TypeGeneral = 1;
        public const int TypeHra = 2;
        public const int TypeDa = 3;

        // NOTE: the database column Allow_Type is text (varchar).
        // ApplicationDbContext converts it to / from this number,
        // so no change is needed in the database.
        [Column("Allow_Type")]
        [Display(Name = "Allowance Type")]
        public int? AllowType { get; set; }

        // Text from the database ("1", "2", "3", empty, NULL) -> number.
        public static int? ParseAllowType(string? value)
        {
            if (int.TryParse(value?.Trim(), out int number))
            {
                return number;
            }

            return null;
        }
    }
}