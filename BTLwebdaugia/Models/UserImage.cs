
namespace BTLwebdaugia.Models;

public class UserImage
{
    public long ImageId { get; set; }

    public long UserId { get; set; }

    public string ImagePath { get; set; } = null!;

    public DateTime UploadedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
