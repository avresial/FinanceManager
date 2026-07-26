using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Components.Features.Identity.Models;

public class LoginModel
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(254, MinimumLength = 5, ErrorMessage = "Email must be between 5 and 254 characters")]
    public string? Login { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(20, MinimumLength = 5, ErrorMessage = "Password must be between 5 and 20 characters")]
    public string? Password { get; set; }
}