namespace FitnessCrm;

// ---------- Domain models (immutable records) ----------

public enum PlanType
{
    Basic,
    Standard,
    Premium
}

public record Client(int Id, string Name, string Email, int Age, bool IsStudent, int TotalVisits);

public record Subscription(int Id, int ClientId, PlanType Plan, DateTime StartDate, DateTime EndDate, int VisitsUsed);

public record Invoice(int SubscriptionId, string ClientName, PlanType Plan,
                      decimal BasePrice, decimal DiscountPercent, decimal FinalPrice);

// Result of recording one visit: both objects are replaced by updated copies
public record VisitUpdate(Client Client, Subscription Subscription);

// ---------- Safe operation sequence ----------
// The constructor is private, so the ONLY way to get a ValidatedSubscription
// is Create(...). Pricing functions accept only this type, so nobody can
// calculate a price for an unchecked subscription.
public sealed class ValidatedSubscription
{
    public Subscription Subscription { get; }
    public Client Client { get; }

    private ValidatedSubscription(Subscription subscription, Client client)
    {
        Subscription = subscription;
        Client = client;
    }

    public static Result<ValidatedSubscription> Create(Subscription subscription, Client client)
    {
        if (subscription.ClientId != client.Id)
            return Result<ValidatedSubscription>.Fail("Subscription does not belong to this client.");

        return FitnessLogic.ValidateClient(client)
            .Bind(c => FitnessLogic.ValidateSubscription(subscription)
                .Map(s => new ValidatedSubscription(s, c)));
    }
}
