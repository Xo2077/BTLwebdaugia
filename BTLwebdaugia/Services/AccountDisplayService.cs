using System.Security.Claims;
using BTLwebdaugia.Data;
using Microsoft.EntityFrameworkCore;

namespace BTLwebdaugia.Services;

public class AccountDisplayService(BidHubDbContext db)
{
    private Task<AccountDisplay?>? account;

    public Task<AccountDisplay?> GetAsync(ClaimsPrincipal user)
    {
        if (!long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Task.FromResult<AccountDisplay?>(null);
        }

        return account ??= db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new AccountDisplay(
                u.Username,
                u.CreatedAt,
                db.UserImages.Where(i => i.UserId == u.UserId)
                    .Select(i => i.ImagePath).FirstOrDefault()))
            .FirstOrDefaultAsync();
    }
}

public record AccountDisplay(string Username, DateTime CreatedAt, string? AvatarPath);
