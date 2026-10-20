using System;
using System.Collections.Generic;

namespace BTLwebdaugia.Models;

public partial class ProductImage
{
    public long ImageId { get; set; }

    public long ProductId { get; set; }

    public string ImagePath { get; set; } = null!;

    public int SortOrder { get; set; }

    public virtual Product Product { get; set; } = null!;
}
