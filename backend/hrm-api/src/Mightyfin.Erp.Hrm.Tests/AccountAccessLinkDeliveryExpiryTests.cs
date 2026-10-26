using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class AccountAccessLinkDeliveryExpiryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 17, 0, 0, TimeSpan.Zero);
    private sealed class DeliveryClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = AccountAccessLinkDeliveryExpiryTests.Now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static SmtpNotificationFallback Sender(DeliveryClock clock) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HRM:NotificationFallback"] = "smtp",
        }).Build(), clock);

    private static OutboxMessage Link(object? expiry, bool includeExpiry = true)
    {
        var payload = new Dictionary<string, object?>
        {
            ["email"] = "recipient@example.test",
            ["account_link"] = "https://erp.example.test/sign-in?token=private-test-token",
        };
        if (includeExpiry) payload["expires_at"] = expiry;
        return new OutboxMessage
        {
            EventType = HrmEventTypes.AccountAccessLink,
            PayloadJson = JsonSerializer.Serialize(payload),
        };
    }

    [Theory]
    [InlineData("2026-10-25T17:00:00+00:00")]
    [InlineData("2026-10-26T17:00:00+00:00")]
    [InlineData("2026-10-26T19:00:00+02:00")]
    [InlineData("not-a-date")]
    [InlineData(null)]
    public async Task ExpiredOrInvalidLinkFailsBeforeSmtpConfiguration(string? expiry)
    {
        var sender = Sender(new DeliveryClock());
        var row = Link(expiry);
        Assert.True(sender.CanDeliver(row));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.DeliverAsync(row, default));
        Assert.Equal("Account access link has expired or has no valid expiry.", error.Message);
        Assert.DoesNotContain("private-test-token", error.Message);
        Assert.DoesNotContain("recipient@example.test", error.Message);
    }

    [Fact]
    public async Task MissingOrNonStringExpiryFailsClosed()
    {
        foreach (var row in new[] { Link(null, false), Link(123), Link(new[] { "2027-01-01" }) })
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Sender(new DeliveryClock()).DeliverAsync(row, default));
            Assert.Equal("Account access link has expired or has no valid expiry.", error.Message);
        }
    }

    [Fact]
    public async Task RetryRechecksTimeEvenAfterEarlierEligibilityCheck()
    {
        var clock = new DeliveryClock();
        var sender = Sender(clock);
        var row = Link(Now.AddMinutes(1).ToString("O"));
        Assert.True(sender.CanDeliver(row));
        clock.Now = Now.AddMinutes(1);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.DeliverAsync(row, default));
        Assert.Equal("Account access link has expired or has no valid expiry.", error.Message);
    }

    [Fact]
    public async Task FutureLinkAndOrdinaryNotificationsStillReachTransportConfiguration()
    {
        var ordinary = new OutboxMessage
        {
            EventType = HrmEventTypes.LeaveRequested,
            PayloadJson = JsonSerializer.Serialize(new { email = "recipient@example.test" }),
        };
        foreach (var row in new[] { Link(Now.AddTicks(1).ToString("O")), ordinary })
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Sender(new DeliveryClock()).DeliverAsync(row, default));
            Assert.Equal("HRM:Smtp:Host is required when SMTP fallback is enabled.", error.Message);
        }
    }
}
