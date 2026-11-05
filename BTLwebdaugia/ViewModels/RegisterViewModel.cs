using System.ComponentModel.DataAnnotations;

namespace BTLwebdaugia.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Please enter a username.")]
    [StringLength(50, MinimumLength = 3,
        ErrorMessage = "Username must be between 3 and 50 characters.")]
    [RegularExpression(@"[a-zA-Z0-9_.-]+",
        ErrorMessage = "Username may only contain letters, numbers, dots, underscores and hyphens.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Range(typeof(bool), "true", "true",
        ErrorMessage = "You must agree to the Terms of Service.")]
    public bool AgreeToTerms { get; set; }
}
