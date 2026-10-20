using System;
using System.Collections.Generic;

namespace BTLwebdaugia.Models;

public partial class Product
{
    public long ProductId { get; set; }

    public long SellerId { get; set; }

    public int CategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string ApprovalStatus { get; set; } = null!;

    public string? RejectionReason { get; set; }

    public long? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public bool HasBeenApproved { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public virtual ICollection<Auction> Auctions { get; set; } = new List<Auction>();

    public virtual Category Category { get; set; } = null!;

    public virtual User? DeletedByNavigation { get; set; }

    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    public virtual User? ReviewedByNavigation { get; set; }

    public virtual User Seller { get; set; } = null!;
}
