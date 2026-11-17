using BlogCms.Domain.Enums;
using BlogCms.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlogCms.Tests;

/// <summary>
/// Donations remain independent from memberships. The persistence path stays
/// available for a later checkout implementation.
/// </summary>
public class DonationServiceTests
{
    [Fact]
    public async Task RecordDonation_AllowsAnonymousDonations()
    {
        await using var db = TestDb.Create();
        var service = new DonationService(db);

        var donation = await service.RecordDonationAsync(
            25m, "EUR", PaymentProvider.Stripe, "pi_1", null, DonationStatus.Completed);

        Assert.Null(donation.UserId);
        Assert.Equal(25m, donation.Amount);
        Assert.Equal("EUR", donation.Currency);
        Assert.Equal(DonationStatus.Completed, donation.Status);
        Assert.Equal(1, await db.Donations.CountAsync());
    }
}