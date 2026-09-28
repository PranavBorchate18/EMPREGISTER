using System.ComponentModel.DataAnnotations;

namespace Employee_Management_System.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Please enter Login ID.")]
        [Display(Name = "Login ID")]
        public string? LoginId { get; set; }
    }
}