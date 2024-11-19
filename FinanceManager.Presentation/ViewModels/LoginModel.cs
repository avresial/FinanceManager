using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Presentation.ViewModels
{
    public class LoginModel
    {
        [Required(AllowEmptyStrings = false)]
        [StringLength(20, MinimumLength = 3, ErrorMessage = "Login must be between 3 and 20 characters")]
        public string? Login { get; set; }

        [Required(AllowEmptyStrings = false)]
        [StringLength(20, MinimumLength = 5, ErrorMessage = "Password must be between 5 and 20 characters")]
        public string? Password { get; set; }
    }
}
