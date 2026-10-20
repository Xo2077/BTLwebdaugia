
using System;
using System.ComponentModel.DataAnnotations;

namespace BTLwebdaugia.ViewModels
{
    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Username is required.")]
        [StringLength(50, MinimumLength = 2)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}
