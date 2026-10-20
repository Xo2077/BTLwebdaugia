using System;
using System.Collections.Generic;

namespace BTLwebdaugia.Models;

public partial class User
{
    public long UserId { get; set; }

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Role { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Auction> Auctions { get; set; } = new List<Auction>();

    public virtual ICollection<Bid> Bids { get; set; } = new List<Bid>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Product> ProductDeletedByNavigations { get; set; } = new List<Product>();

    public virtual ICollection<Product> ProductReviewedByNavigations { get; set; } = new List<Product>();

    public virtual ICollection<Product> ProductSellers { get; set; } = new List<Product>();

    public virtual Wallet? Wallet { get; set; }
}
