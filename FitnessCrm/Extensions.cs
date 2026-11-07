using System.Globalization;

namespace FitnessCrm;

public static class Extensions
{
    // true if the date is inside the subscription period
    public static bool IsActiveOn(this Subscription sub, DateTime date)
    {
        return date.Date >= sub.StartDate.Date && date.Date <= sub.EndDate.Date;
    }

    public static int DaysLeft(this Subscription sub, DateTime today)
    {
        return (sub.EndDate.Date - today.Date).Days;
    }

    public static string ToMoney(this decimal amount)
    {
        return amount.ToString("N2", CultureInfo.InvariantCulture) + " KZT";
    }

    // null -> None, value -> Some
    public static Maybe<T> ToMaybe<T>(this T? value) where T : class
    {
        return value is null ? Maybe<T>.None() : Maybe<T>.Some(value);
    }

    // ---------- Lookups return Maybe (explicit "not found") ----------

    public static Maybe<Client> FindClient(this IEnumerable<Client> clients, int id)
    {
        return clients.FirstOrDefault(c => c.Id == id).ToMaybe();
    }

    public static Maybe<Subscription> FindSubscription(this IEnumerable<Subscription> subs, int id)
    {
        return subs.FirstOrDefault(s => s.Id == id).ToMaybe();
    }

    public static Maybe<Subscription> FindActiveSubscription(this IEnumerable<Subscription> subs, int clientId, DateTime today)
    {
        return subs.FirstOrDefault(s => s.ClientId == clientId && s.IsActiveOn(today)).ToMaybe();
    }

    // ---------- Replace without changing the original list ----------

    public static List<Client> ReplaceClient(this IEnumerable<Client> clients, Client updated)
    {
        return clients.Select(c => c.Id == updated.Id ? updated : c).ToList();
    }

    public static List<Subscription> ReplaceSubscription(this IEnumerable<Subscription> subs, Subscription updated)
    {
        return subs.Select(s => s.Id == updated.Id ? updated : s).ToList();
    }
}
