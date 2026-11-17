using BlogCms.Domain.Entities;
using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Data;

namespace BlogCms.Infrastructure.Payments;

/// <summary>
/// Records one-time donations independently from the membership subscription
/// flow. The donation UI is still a placeholder, but the persistence path remains
/// available for a later checkout implementation.
/// </summary>
public interface IDonationService
{
    Task<Donation> RecordDonationAsync(
        decimal amount, string currency, PaymentProvider provider, string transactionId,
        Guid? userId, DonationStatus status, CancellationToken cancellationToken = default);
}

public sealed class DonationService : IDonationService
{
    private readonly AppDbContext _db;

    public DonationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Donation> RecordDonationAsync(
        decimal amount, string currency, PaymentProvider provider, string transactionId,
        Guid? userId, DonationStatus status, CancellationToken cancellationToken = default)
    {
        var donation = new Donation
        {
            Amount = amount,
            Currency = currency,
            Provider = provider,
            ProviderTransactionId = transactionId,
            UserId = userId,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        _db.Donations.Add(donation);
        await _db.SaveChangesAsync(cancellationToken);
        return donation;
    }
}