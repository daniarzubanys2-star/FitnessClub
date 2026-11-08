namespace FitnessCrm;

// Pure functions only: no Console, no files, no global state.
// Same input always gives the same output.
public static class FitnessLogic
{
    // ---------- Validation ----------

    public static Result<Client> ValidateClient(Client client)
    {
        if (string.IsNullOrWhiteSpace(client.Name))
            return Result<Client>.Fail("Client name is required.");
        if (string.IsNullOrWhiteSpace(client.Email) || !client.Email.Contains('@'))
            return Result<Client>.Fail("Client email is not valid.");
        if (client.Age < 14 || client.Age > 100)
            return Result<Client>.Fail("Client age must be between 14 and 100.");
        if (client.TotalVisits < 0)
            return Result<Client>.Fail("Total visits cannot be negative.");

        return Result<Client>.Ok(client);
    }

    public static Result<Subscription> ValidateSubscription(Subscription sub)
    {
        if (!Enum.IsDefined(sub.Plan))
            return Result<Subscription>.Fail("Unknown plan type.");
        if (sub.EndDate.Date <= sub.StartDate.Date)
            return Result<Subscription>.Fail("End date must be after start date.");
        if (CountMonths(sub) > 12)
            return Result<Subscription>.Fail("A subscription cannot be longer than 12 months.");
        if (sub.VisitsUsed < 0)
            return Result<Subscription>.Fail("Visits used cannot be negative.");

        return Result<Subscription>.Ok(sub);
    }

    public static Result<Client> CheckEmailIsFree(IEnumerable<Client> clients, Client newClient)
    {
        var taken = clients.Any(c => string.Equals(c.Email, newClient.Email, StringComparison.OrdinalIgnoreCase));
        return taken
            ? Result<Client>.Fail("A client with this email already exists.")
            : Result<Client>.Ok(newClient);
    }

    // A client cannot have two active subscriptions at the same time
    public static Result<Client> CheckNoOverlap(IEnumerable<Subscription> subs, Client client, DateTime startDate)
    {
        var overlap = subs.Any(s => s.ClientId == client.Id && s.IsActiveOn(startDate));
        return overlap
            ? Result<Client>.Fail("Client already has an active subscription on that date.")
            : Result<Client>.Ok(client);
    }

    // ---------- Pricing ----------

    // Pattern matching (switch expression) on the plan type
    public static decimal GetMonthlyPrice(PlanType plan) => plan switch
    {
        PlanType.Basic => 15000m,
        PlanType.Standard => 25000m,
        PlanType.Premium => 40000m,
        _ => throw new ArgumentOutOfRangeException(nameof(plan), "Unknown plan") // never happens after validation
    };

    public static int GetVisitLimit(PlanType plan) => plan switch
    {
        PlanType.Basic => 12,
        PlanType.Standard => 30,
        PlanType.Premium => int.MaxValue, // unlimited
        _ => 0
    };

    // A month is counted as 30 days, started months are rounded up
    public static int CountMonths(Subscription sub)
    {
        var days = (sub.EndDate.Date - sub.StartDate.Date).Days;
        return Math.Max(1, (int)Math.Ceiling(days / 30.0));
    }

    public static decimal CalculateBasePrice(Subscription sub)
    {
        return GetMonthlyPrice(sub.Plan) * CountMonths(sub);
    }

    public static decimal ApplyDiscount(decimal price, decimal percent)
    {
        return Math.Round(price * (100 - percent) / 100, 2);
    }

    // ---------- Discount rules (these are passed as Func<Client, decimal>) ----------

    public static decimal NoDiscount(Client client) => 0m;

    public static decimal StudentDiscount(Client client) => client.IsStudent ? 15m : 0m;

    // Relational patterns
    public static decimal LoyaltyDiscount(Client client) => client.TotalVisits switch
    {
        >= 100 => 20m,
        >= 50 => 10m,
        _ => 0m
    };

    public static decimal BestDiscount(Client client)
    {
        return Math.Max(StudentDiscount(client), LoyaltyDiscount(client));
    }

    // ---------- Visits and renewal ----------

    public static Result<VisitUpdate> RecordVisit(Client client, Subscription sub, DateTime today)
    {
        if (!sub.IsActiveOn(today))
            return Result<VisitUpdate>.Fail("Subscription is not active today.");
        if (sub.VisitsUsed >= GetVisitLimit(sub.Plan))
            return Result<VisitUpdate>.Fail("Visit limit of this subscription is reached.");

        // 'with' makes updated copies, the originals stay unchanged
        var newSub = sub with { VisitsUsed = sub.VisitsUsed + 1 };
        var newClient = client with { TotalVisits = client.TotalVisits + 1 };
        return Result<VisitUpdate>.Ok(new VisitUpdate(newClient, newSub));
    }

    public static Result<Subscription> Renew(Subscription sub, int months, DateTime today)
    {
        if (months < 1 || months > 12)
            return Result<Subscription>.Fail("Renewal must be between 1 and 12 months.");
        if (today.Date > sub.EndDate.Date.AddDays(30))
            return Result<Subscription>.Fail("Subscription expired more than 30 days ago. Please buy a new one.");

        // if still active, continue from the old end date, otherwise from today
        var startFrom = sub.EndDate.Date > today.Date ? sub.EndDate.Date : today.Date;
        return Result<Subscription>.Ok(sub with { EndDate = startFrom.AddDays(30 * months) });
    }

    // Pattern matching with relational patterns on days left
    public static string GetStatus(Subscription sub, DateTime today)
    {
        if (today.Date < sub.StartDate.Date)
            return "Not started";

        return (sub.EndDate.Date - today.Date).Days switch
        {
            < 0 => "Expired",
            <= 7 => "Expiring soon",
            _ => "Active"
        };
    }
}
