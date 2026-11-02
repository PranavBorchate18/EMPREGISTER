using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Employee_Management_System.Models
{
    [Table("OrganisationMaster")]
    public class OrganisationMaster
    {
        [Key]
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Column("Code")]
        [Display(Name = "Organisation Code")]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter Organisation Name.")]
        [StringLength(200)]
        [Column("OrganisationName")]
        [Display(Name = "Organisation Name")]
        public string OrganisationName { get; set; } = string.Empty;

        [Required]
        [StringLength(128)]
        [Column("DatabaseName")]
        [Display(Name = "Database Name")]
        public string DatabaseName { get; set; } = string.Empty;
    }
}