
using System.ComponentModel.DataAnnotations;

namespace BTLwebdaugia.ViewModels;

public class PaymentViewModel
{
    [Range(1, long.MaxValue,
        ErrorMessage = "Invalid order.")]
    public long OrderId { get; set; }
}
