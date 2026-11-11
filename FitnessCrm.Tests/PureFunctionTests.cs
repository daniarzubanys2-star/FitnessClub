using Xunit;

namespace FitnessCrm.Tests;

// Tests for pure functions, domain validation, Result/Maybe, reports.
public class PureFunctionTests
{
    private static readonly DateTime Today = new(2026, 11, 7);

    private static Client MakeClient(int id = 1, string name = "Test Client", int visits = 0, bool student = false)
        => new(id, name, "test@example.com", 25, student, visits);

    private static Subscription MakeSub(int id = 1, int clientId = 1, PlanType plan = PlanType.Basic,
                                        int startOffset = -5, int endOffset = 25, int visitsUsed = 0)
        => new(id, clientId, plan, Today.AddDays(startOffset), Today.AddDays(endOffset), visitsUsed);

    // ---------- validation ----------

    [Fact]
    public void ValidateClient_ValidClient_Succeeds()
    {
        var result = FitnessLogic.ValidateClient(MakeClient());
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateClient_EmptyName_Fails()
    {
        var result = FitnessLogic.ValidateClient(MakeClient(name: "  "));
        Assert.False(result.IsSuccess);
        Assert.Equal("Client name is required.", result.Error);
    }

    [Theory]
    [InlineData(13, false)]   // boundary: too young
    [InlineData(14, true)]    // boundary: minimum
    [InlineData(100, true)]   // boundary: maximum
    [InlineData(101, false)]  // boundary: too old
    public void ValidateClient_AgeBoundaries(int age, bool expectedOk)
    {
        var client = new Client(1, "Test", "t@example.com", age, false, 0);
        Assert.Equal(expectedOk, FitnessLogic.ValidateClient(client).IsSuccess);
    }

    // ---------- pricing ----------

    [Theory]
    [InlineData(1, 1)]
    [InlineData(30, 1)]   // exactly one month
    [InlineData(31, 2)]   // one day more -> second month started
    [InlineData(60, 2)]
    [InlineData(61, 3)]
    public void CountMonths_RoundsUpStartedMonths(int days, int expectedMonths)
    {
        var sub = new Subscription(1, 1, PlanType.Basic, Today, Today.AddDays(days), 0);
        Assert.Equal(expectedMonths, FitnessLogic.CountMonths(sub));
    }

    [Fact]
    public void CalculateBasePrice_StandardTwoMonths_Is50000()
    {
        var sub = new Subscription(1, 1, PlanType.Standard, Today, Today.AddDays(60), 0);
        Assert.Equal(50000m, FitnessLogic.CalculateBasePrice(sub));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(49, 0)]    // boundary below
    [InlineData(50, 10)]   // boundary
    [InlineData(99, 10)]
    [InlineData(100, 20)]  // boundary
    public void LoyaltyDiscount_Boundaries(int visits, int expectedPercent)
    {
        var client = MakeClient(visits: visits);
        Assert.Equal((decimal)expectedPercent, FitnessLogic.LoyaltyDiscount(client));
    }

    // ---------- safe operation sequence ----------

    [Fact]
    public void ValidatedSubscription_WrongClient_IsRejected()
    {
        var client = MakeClient(id: 1);
        var sub = MakeSub(clientId: 2); // belongs to somebody else

        var result = ValidatedSubscription.Create(sub, client);

        Assert.False(result.IsSuccess);
        Assert.Contains("does not belong", result.Error);
    }

    [Fact]
    public void ValidatedSubscription_EndBeforeStart_IsRejected()
    {
        var sub = MakeSub(startOffset: 10, endOffset: 5);

        var result = ValidatedSubscription.Create(sub, MakeClient());

        Assert.False(result.IsSuccess);
        Assert.Equal("End date must be after start date.", result.Error);
    }

    // ---------- Result and Maybe ----------

    [Fact]
    public void Result_Map_OnSuccess_ChangesValue()
    {
        var result = Result<int>.Ok(5).Map(x => x * 2);
        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value);
    }

    [Fact]
    public void Result_Bind_AfterFailure_SkipsNextStage()
    {
        var nextStageRan = false;

        var result = Result<int>.Fail("first stage failed")
            .Bind(x =>
            {
                nextStageRan = true;
                return Result<int>.Ok(x + 1);
            });

        Assert.False(result.IsSuccess);
        Assert.Equal("first stage failed", result.Error);
        Assert.False(nextStageRan);
    }

    [Fact]
    public void FindClient_MissingId_ReturnsNone()
    {
        var clients = new List<Client> { MakeClient(id: 1) };
        Assert.False(clients.FindClient(99).HasValue);
    }

    [Fact]
    public void FindClient_EmptyList_ReturnsNone()
    {
        var clients = new List<Client>();
        Assert.False(clients.FindClient(1).HasValue);
    }

    [Fact]
    public void FindClient_ExistingId_ReturnsSome()
    {
        var clients = new List<Client> { MakeClient(id: 7, name: "Found") };
        var found = clients.FindClient(7);
        Assert.True(found.HasValue);
        Assert.Equal("Found", found.Value.Name);
    }

    // ---------- visits, renewal, status ----------

    [Fact]
    public void RecordVisit_ExpiredSubscription_Fails()
    {
        var sub = MakeSub(startOffset: -40, endOffset: -1);
        var result = FitnessLogic.RecordVisit(MakeClient(), sub, Today);
        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData(11, true)]    // one visit left (Basic limit is 12)
    [InlineData(12, false)]   // limit reached
    public void RecordVisit_BasicLimitBoundary(int visitsUsed, bool expectedOk)
    {
        var sub = MakeSub(plan: PlanType.Basic, visitsUsed: visitsUsed);
        var result = FitnessLogic.RecordVisit(MakeClient(), sub, Today);
        Assert.Equal(expectedOk, result.IsSuccess);
    }

    [Fact]
    public void RecordVisit_DoesNotChangeOriginalObjects()
    {
        var client = MakeClient(visits: 3);
        var sub = MakeSub(visitsUsed: 2);

        var result = FitnessLogic.RecordVisit(client, sub, Today);

        Assert.Equal(4, result.Value.Client.TotalVisits);
        Assert.Equal(3, result.Value.Subscription.VisitsUsed);
        Assert.Equal(3, client.TotalVisits);   // original is unchanged
        Assert.Equal(2, sub.VisitsUsed);
    }

    [Fact]
    public void Renew_ExpiredMoreThan30DaysAgo_Fails()
    {
        var sub = MakeSub(startOffset: -90, endOffset: -31);
        var result = FitnessLogic.Renew(sub, 1, Today);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Renew_ActiveSubscription_ExtendsFromOldEndDate()
    {
        var sub = MakeSub(endOffset: 10);
        var result = FitnessLogic.Renew(sub, 1, Today);
        Assert.True(result.IsSuccess);
        Assert.Equal(Today.AddDays(40), result.Value.EndDate);
    }

    [Theory]
    [InlineData(-1, "Expired")]
    [InlineData(0, "Expiring soon")]
    [InlineData(7, "Expiring soon")]
    [InlineData(8, "Active")]
    public void GetStatus_UsesDaysLeft(int endOffset, string expected)
    {
        var sub = MakeSub(startOffset: -30, endOffset: endOffset);
        Assert.Equal(expected, FitnessLogic.GetStatus(sub, Today));
    }

    // ---------- reports ----------

    [Fact]
    public void RevenueByPlan_EmptyList_ReturnsEmptyReport()
    {
        var rows = Reports.RevenueByPlan(new List<Subscription>());
        Assert.Empty(rows);
    }

    [Fact]
    public void RevenueByPlan_GroupsAndSumsPerPlan()
    {
        var subs = new List<Subscription>
        {
            new(1, 1, PlanType.Basic, Today, Today.AddDays(30), 4),
            new(2, 2, PlanType.Basic, Today, Today.AddDays(30), 6),
            new(3, 3, PlanType.Premium, Today, Today.AddDays(30), 0)
        };

        var rows = Reports.RevenueByPlan(subs);

        Assert.Equal(2, rows.Count);
        Assert.Equal(PlanType.Premium, rows[0].Plan);       // 40000 is bigger than 30000
        Assert.Equal(30000m, rows[1].TotalListPrice);
        Assert.Equal(2, rows[1].SubscriptionCount);
        Assert.Equal(5.0, rows[1].AverageVisits);
    }

    [Fact]
    public void ExpiringSoon_SameEndDate_SortedByClientName()
    {
        var clients = new List<Client>
        {
            MakeClient(id: 1, name: "Zhanar"),
            MakeClient(id: 2, name: "Aibek"),
            MakeClient(id: 3, name: "Third")
        };
        var subs = new List<Subscription>
        {
            MakeSub(id: 1, clientId: 1, endOffset: 3),
            MakeSub(id: 2, clientId: 2, endOffset: 3),
            MakeSub(id: 3, clientId: 3, endOffset: 1),
            MakeSub(id: 4, clientId: 1, endOffset: 20)   // too far away, filtered out
        };

        var rows = Reports.ExpiringSoon(clients, subs, Today, 5);

        Assert.Equal(new[] { "Third", "Aibek", "Zhanar" }, rows.Select(r => r.ClientName).ToArray());
    }
}
