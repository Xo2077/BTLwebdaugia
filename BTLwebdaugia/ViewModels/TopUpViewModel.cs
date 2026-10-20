
using System.ComponentModel.DataAnnotations;

namespace BTLwebdaugia.ViewModels;

public class TopUpViewModel
{
    [Range(typeof(decimal), "1", "999999999999999999",
        ErrorMessage = "Top-up amount must be greater than zero.")]
    public decimal Amount { get; set; }
}
    