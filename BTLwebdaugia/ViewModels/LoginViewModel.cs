
using System.ComponentModel.DataAnnotations;

namespace BTLwebdaugia.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password.")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}
