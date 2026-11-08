namespace FitnessCrm;

public record PlanRevenueRow(PlanType Plan, int SubscriptionCount, decimal TotalListPrice, double AverageVisits);
public record ExpiringRow(string ClientName, PlanType Plan, DateTime EndDate, int DaysLeft);

public static class Reports
{
    // Lazy processing: yield return. Nothing runs until somebody enumerates the result.
    public static IEnumerable<Subscription> StreamExpiring(IEnumerable<Subscription> subs, DateTime today, int days)
    {
        foreach (var sub in subs)
        {
            if (sub.IsActiveOn(today) && sub.DaysLeft(today) <= days)
                yield return sub;
        }
    }

    // LINQ report 1: GroupBy + Count/Sum/Average + ordering with a tie-breaker
    public static List<PlanRevenueRow> RevenueByPlan(IEnumerable<Subscription> subs)
    {
        return subs
            .GroupBy(s => s.Plan)
            .Select(g => new PlanRevenueRow(
                g.Key,
                g.Count(),
                g.Sum(s => FitnessLogic.CalculateBasePrice(s)),
                g.Average(s => s.VisitsUsed)))
            .OrderByDescending(r => r.TotalListPrice)
            .ThenBy(r => r.Plan.ToString())
            .ToList();
    }

    // LINQ report 2: filter (lazy stream) + Join + projection + order by date then name
    public static List<ExpiringRow> ExpiringSoon(IEnumerable<Client> clients, IEnumerable<Subscription> subs, DateTime today, int days)
    {
        return StreamExpiring(subs, today, days)
            .Join(clients,
                  s => s.ClientId,
                  c => c.Id,
                  (s, c) => new ExpiringRow(c.Name, s.Plan, s.EndDate, s.DaysLeft(today)))
            .OrderBy(r => r.EndDate)
            .ThenBy(r => r.ClientName)
            .ToList();
    }

    // Text lines for printing / exporting (still pure: just builds strings)
    public static List<string> RevenueLines(IEnumerable<PlanRevenueRow> rows)
    {
        return rows
            .Select(r => $"{r.Plan,-10} {r.SubscriptionCount,3} subs  {r.TotalListPrice.ToMoney(),18}  avg visits: {r.AverageVisits:F1}")
            .ToList();
    }

    public static List<string> ExpiringLines(IEnumerable<ExpiringRow> rows)
    {
        return rows
            .Select(r => $"{r.ClientName,-22} {r.Plan,-10} ends {r.EndDate:yyyy-MM-dd}  ({r.DaysLeft} days left)")
            .ToList();
    }
}
