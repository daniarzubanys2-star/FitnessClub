namespace FitnessCrm;

// Outputs of the pipeline stages. Each stage returns a bigger record
// that is the input of the next stage.
public record PricedSubscription(ValidatedSubscription Validated, decimal BasePrice);
public record DiscountedSubscription(PricedSubscription Priced, decimal DiscountPercent, decimal FinalPrice);

// Results of whole workflows (new lists, the old lists are not changed)
public record PurchaseResult(Invoice Invoice, List<Subscription> Subscriptions);
public record VisitResult(List<Client> Clients, List<Subscription> Subscriptions);

public static class Pipeline
{
    // ================= Pipeline stages =================

    // Stage 1: validate -> ValidatedSubscription.Create(subscription, client)

    // Stage 2: calculate base price
    public static PricedSubscription PriceStage(ValidatedSubscription validated)
    {
        return new PricedSubscription(validated, FitnessLogic.CalculateBasePrice(validated.Subscription));
    }

    // Stage 3: apply discount. The rule is a Func, so the business rule can be swapped.
    public static Result<DiscountedSubscription> DiscountStage(PricedSubscription priced, Func<Client, decimal> discountRule)
    {
        var percent = discountRule(priced.Validated.Client);
        if (percent < 0 || percent > 100)
            return Result<DiscountedSubscription>.Fail($"Discount rule returned an invalid percent: {percent}");

        var finalPrice = FitnessLogic.ApplyDiscount(priced.BasePrice, percent);
        return Result<DiscountedSubscription>.Ok(new DiscountedSubscription(priced, percent, finalPrice));
    }

    // Stage 4: create the invoice
    public static Invoice InvoiceStage(DiscountedSubscription discounted)
    {
        var sub = discounted.Priced.Validated.Subscription;
        var client = discounted.Priced.Validated.Client;
        return new Invoice(sub.Id, client.Name, sub.Plan,
                           discounted.Priced.BasePrice, discounted.DiscountPercent, discounted.FinalPrice);
    }

    // Whole pipeline. If any Bind fails, the later stages are skipped.
    public static Result<Invoice> SellSubscription(Subscription sub, Client client, Func<Client, decimal> discountRule)
    {
        return ValidatedSubscription.Create(sub, client)        // stage 1 (can fail)
            .Map(PriceStage)                                    // stage 2
            .Bind(priced => DiscountStage(priced, discountRule))// stage 3 (can fail)
            .Map(InvoiceStage);                                 // stage 4
    }

    // ================= Workflows =================

    // Workflow 1: register a new client
    public static Result<List<Client>> RegisterClient(IEnumerable<Client> clients, string name, string email, int age, bool isStudent)
    {
        var list = clients.ToList();
        var newId = list.Count == 0 ? 1 : list.Max(c => c.Id) + 1;
        var newClient = new Client(newId, name.Trim(), email.Trim(), age, isStudent, 0);

        return FitnessLogic.ValidateClient(newClient)
            .Bind(c => FitnessLogic.CheckEmailIsFree(list, c))
            .Map(c => list.Append(c).ToList());
    }

    // Workflow 2: buy a subscription (find client -> check overlap -> pipeline -> add subscription)
    public static Result<PurchaseResult> BuySubscription(
        IEnumerable<Client> clients, IEnumerable<Subscription> subscriptions,
        int clientId, PlanType plan, DateTime startDate, int months, Func<Client, decimal> discountRule)
    {
        if (months < 1 || months > 12)
            return Result<PurchaseResult>.Fail("Months must be between 1 and 12.");

        var subList = subscriptions.ToList();
        var newId = subList.Count == 0 ? 1 : subList.Max(s => s.Id) + 1;

        return clients.FindClient(clientId)
            .ToResult($"Client {clientId} was not found.")
            .Bind(client => FitnessLogic.CheckNoOverlap(subList, client, startDate))
            .Bind(client => SellNewSubscription(subList, newId, client, plan, startDate, months, discountRule));
    }

    private static Result<PurchaseResult> SellNewSubscription(
        List<Subscription> subList, int newId, Client client, PlanType plan,
        DateTime startDate, int months, Func<Client, decimal> discountRule)
    {
        var newSub = new Subscription(newId, client.Id, plan, startDate, startDate.AddDays(30 * months), 0);
        return SellSubscription(newSub, client, discountRule)
            .Map(invoice => new PurchaseResult(invoice, subList.Append(newSub).ToList()));
    }

    // Workflow 3: record a gym visit (find client -> find active subscription -> check limit)
    public static Result<VisitResult> RecordVisit(
        IEnumerable<Client> clients, IEnumerable<Subscription> subscriptions, int clientId, DateTime today)
    {
        var clientList = clients.ToList();
        var subList = subscriptions.ToList();

        return clientList.FindClient(clientId)
            .ToResult($"Client {clientId} was not found.")
            .Bind(client => subList.FindActiveSubscription(clientId, today)
                .ToResult($"Client {clientId} has no active subscription today.")
                .Bind(sub => FitnessLogic.RecordVisit(client, sub, today)))
            .Map(update => new VisitResult(
                clientList.ReplaceClient(update.Client),
                subList.ReplaceSubscription(update.Subscription)));
    }

    // Workflow 4: renew a subscription
    public static Result<List<Subscription>> RenewSubscription(
        IEnumerable<Subscription> subscriptions, int subscriptionId, int months, DateTime today)
    {
        var subList = subscriptions.ToList();

        return subList.FindSubscription(subscriptionId)
            .ToResult($"Subscription {subscriptionId} was not found.")
            .Bind(sub => FitnessLogic.Renew(sub, months, today))
            .Map(updated => subList.ReplaceSubscription(updated));
    }
}
