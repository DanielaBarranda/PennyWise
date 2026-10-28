using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Data;
using PennyWise.Api.Models;

namespace PennyWise.Api.Auth;

// Budgets and expenses created before accounts existed have no owner (UserId is
// null) and are hidden from everyone. They are handed to exactly one account:
// the one whose email matches the "PennyWise:LegacyOwnerEmail" setting, the first
// time it registers or logs in. With no setting, nothing is ever claimed.
// Nothing is deleted; setting UserId back to null undoes a claim.
public sealed class LegacyDataClaimer(PennyWiseDbContext db, IConfiguration configuration, ILogger<LegacyDataClaimer> logger)
{
    public async Task ClaimIfOwnerAsync(User user)
    {
        var ownerEmail = configuration["PennyWise:LegacyOwnerEmail"];
        if (string.IsNullOrWhiteSpace(ownerEmail) || AuthRules.Normalize(ownerEmail) != user.NormalizedEmail) return;

        var userId = user.Id;
        var expenses = await db.Expenses
            .Where(e => e.UserId == null)
            .ExecuteUpdateAsync(set => set.SetProperty(e => e.UserId, userId));

        // A month this user already budgeted keeps their own budget; the older
        // one stays unowned (and preserved) instead of breaking the one-per-month rule.
        var budgets = await db.Budgets
            .Where(b => b.UserId == null && !db.Budgets.Any(o => o.UserId == userId && o.Month == b.Month))
            .ExecuteUpdateAsync(set => set.SetProperty(b => b.UserId, userId));

        if (expenses + budgets > 0)
        {
            logger.LogInformation("Assigned {Budgets} budget(s) and {Expenses} expense(s) created before accounts to user {UserId}",
                budgets, expenses, userId);
        }
    }
}
