
using System.ComponentModel.DataAnnotations;

namespace Employee_Management_System.Models
{
    public class LoginViewModel
    {
        // Login ID
        [Required(ErrorMessage = "Please enter Login ID.")]
        [Display(Name = "Login ID")]
        public string? LoginId { get; set; }

        // Password
        [Required(ErrorMessage = "Please enter Password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string? Password { get; set; }
    }
}
