using Xunit;

namespace FitnessCrm.Tests;

// Tests for complete workflows (several stages together)
public class WorkflowTests
{
    private static readonly DateTime Today = new(2026, 11, 7);

    [Fact]
    public void BuySubscription_StudentWithDiscount_ReturnsInvoiceAndNewList()
    {
        var clients = new List<Client> { new(1, "Aigerim", "a@example.com", 21, true, 10) };
        var subs = new List<Subscription>();

        var result = Pipeline.BuySubscription(clients, subs, 1, PlanType.Basic, Today, 2, FitnessLogic.StudentDiscount);

        Assert.True(result.IsSuccess);
        Assert.Equal(30000m, result.Value.Invoice.BasePrice);   // 2 months * 15000
        Assert.Equal(15m, result.Value.Invoice.DiscountPercent);
        Assert.Equal(25500m, result.Value.Invoice.FinalPrice);
        Assert.Single(result.Value.Subscriptions);
        Assert.Empty(subs);                                      // the caller's list is not changed
    }

    [Fact]
    public void BuySubscription_UnknownClient_FailsAndSkipsDiscountStage()
    {
        var discountRuleWasCalled = false;
        Func<Client, decimal> spyRule = c =>
        {
            discountRuleWasCalled = true;
            return 0m;
        };

        var result = Pipeline.BuySubscription(
            new List<Client>(), new List<Subscription>(), 99, PlanType.Basic, Today, 1, spyRule);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
        Assert.False(discountRuleWasCalled);   // failure skipped the later stages
    }

    [Fact]
    public void BuySubscription_ClientAlreadyHasActiveSubscription_Fails()
    {
        var clients = new List<Client> { new(1, "Aigerim", "a@example.com", 21, false, 0) };
        var subs = new List<Subscription>
        {
            new(1, 1, PlanType.Basic, Today.AddDays(-5), Today.AddDays(25), 0)
        };

        var result = Pipeline.BuySubscription(clients, subs, 1, PlanType.Premium, Today, 1, FitnessLogic.NoDiscount);

        Assert.False(result.IsSuccess);
        Assert.Contains("already has an active subscription", result.Error);
    }

    [Fact]
    public void SellSubscription_DiscountRuleReturns150_FailsInDiscountStage()
    {
        var client = new Client(1, "Test", "t@example.com", 30, false, 0);
        var sub = new Subscription(1, 1, PlanType.Basic, Today, Today.AddDays(30), 0);
        Func<Client, decimal> brokenRule = c => 150m;

        var result = Pipeline.SellSubscription(sub, client, brokenRule);

        Assert.False(result.IsSuccess);
        Assert.Contains("invalid percent", result.Error);
    }

    [Fact]
    public void RecordVisit_ActiveSubscription_UpdatesClientAndSubscription()
    {
        var clients = new List<Client> { new(1, "Aigerim", "a@example.com", 21, false, 5) };
        var subs = new List<Subscription>
        {
            new(1, 1, PlanType.Standard, Today.AddDays(-5), Today.AddDays(25), 2)
        };

        var result = Pipeline.RecordVisit(clients, subs, 1, Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value.Clients[0].TotalVisits);
        Assert.Equal(3, result.Value.Subscriptions[0].VisitsUsed);
        Assert.Equal(5, clients[0].TotalVisits);   // original list still has the old value
    }

    [Fact]
    public void RecordVisit_ClientWithoutActiveSubscription_Fails()
    {
        var clients = new List<Client> { new(1, "Aigerim", "a@example.com", 21, false, 5) };

        var result = Pipeline.RecordVisit(clients, new List<Subscription>(), 1, Today);

        Assert.False(result.IsSuccess);
        Assert.Contains("no active subscription", result.Error);
    }

    [Fact]
    public void RegisterClient_DuplicateEmail_Fails()
    {
        var clients = new List<Client> { new(1, "Aigerim", "a@example.com", 21, false, 0) };

        var result = Pipeline.RegisterClient(clients, "Another", "A@Example.com", 30, false);

        Assert.False(result.IsSuccess);
        Assert.Contains("already exists", result.Error);
    }
}
