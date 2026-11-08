using System.Globalization;

namespace FitnessCrm;

// Console interface = system boundary (input/output). All business logic is in the other files.
public class Program
{
    static string dataFolder = "";
    static List<Client> clients = new();
    static List<Subscription> subs = new();
    static DateTime today = DateTime.Today;

    static string ClientsPath => Path.Combine(dataFolder, "clients.json");
    static string SubsPath => Path.Combine(dataFolder, "subscriptions.json");

    static void Main(string[] args)
    {
        // data folder can be given as the first argument, default = "data" next to the program
        dataFolder = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "data");
        LoadData();

        var running = true;
        while (running)
        {
            PrintMenu();
            var choice = Console.ReadLine()?.Trim();
            Console.WriteLine();

            switch (choice)
            {
                case "1": RegisterClientAction(); break;
                case "2": BuySubscriptionAction(); break;
                case "3": RecordVisitAction(); break;
                case "4": RenewAction(); break;
                case "5": ShowRevenueReport(); break;
                case "6": ShowExpiringReport(); break;
                case "7": ListClients(); break;
                case "8": LazyDemo(); break;
                case "9": SaveData(); break;
                case "10": ExportReports(); break;
                case "11": ChangeDate(); break;
                case "0": running = false; break;
                default: Console.WriteLine("Unknown command."); break;
            }
        }
    }

    static void PrintMenu()
    {
        Console.WriteLine();
        Console.WriteLine($"===== Fitness Club CRM  (working date: {today:yyyy-MM-dd}) =====");
        Console.WriteLine(" 1. Register client");
        Console.WriteLine(" 2. Buy subscription");
        Console.WriteLine(" 3. Record visit");
        Console.WriteLine(" 4. Renew subscription");
        Console.WriteLine(" 5. Report: revenue by plan");
        Console.WriteLine(" 6. Report: expiring subscriptions");
        Console.WriteLine(" 7. List clients and subscriptions");
        Console.WriteLine(" 8. Lazy processing demo");
        Console.WriteLine(" 9. Save data");
        Console.WriteLine("10. Export reports to file");
        Console.WriteLine("11. Change working date");
        Console.WriteLine(" 0. Exit");
        Console.Write("Choose: ");
    }

    // ---------- Loading / saving ----------

    static void LoadData()
    {
        clients = FileStorage.LoadClients(ClientsPath).Match(
            list => list,
            error => { Console.WriteLine("Warning: " + error); return new List<Client>(); });

        subs = FileStorage.LoadSubscriptions(SubsPath).Match(
            list => list,
            error => { Console.WriteLine("Warning: " + error); return new List<Subscription>(); });

        Console.WriteLine($"Loaded {clients.Count} clients and {subs.Count} subscriptions.");
    }

    static void SaveData()
    {
        Console.WriteLine(FileStorage.SaveClients(ClientsPath, clients).Match(ok => ok, err => "Failed: " + err));
        Console.WriteLine(FileStorage.SaveSubscriptions(SubsPath, subs).Match(ok => ok, err => "Failed: " + err));
    }

    static void ExportReports()
    {
        var lines = new List<string> { $"Fitness Club report, date {today:yyyy-MM-dd}", "", "Revenue by plan (list price):" };
        lines.AddRange(Reports.RevenueLines(Reports.RevenueByPlan(subs)));
        lines.Add("");
        lines.Add("Expiring in 10 days:");
        lines.AddRange(Reports.ExpiringLines(Reports.ExpiringSoon(clients, subs, today, 10)));

        var path = Path.Combine(dataFolder, "report.txt");
        Console.WriteLine(FileStorage.ExportLines(path, lines).Match(ok => ok, err => "Failed: " + err));
    }

    // ---------- Workflows ----------

    static void RegisterClientAction()
    {
        Console.Write("Name: ");
        var name = Console.ReadLine() ?? "";
        Console.Write("Email: ");
        var email = Console.ReadLine() ?? "";
        var age = ReadInt("Age: ");
        if (age is null) return;
        Console.Write("Student? (y/n): ");
        var isStudent = (Console.ReadLine() ?? "").Trim().ToLower() == "y";

        var result = Pipeline.RegisterClient(clients, name, email, age.Value, isStudent);
        if (result.IsSuccess)
        {
            clients = result.Value;
            Console.WriteLine("Client registered. Id = " + clients.Last().Id);
        }
        else
        {
            Console.WriteLine("Could not register client: " + result.Error);
        }
    }

    static void BuySubscriptionAction()
    {
        var clientId = ReadInt("Client id: ");
        if (clientId is null) return;
        var plan = ReadPlan();
        if (plan is null) return;
        var start = ReadDate("Start date (yyyy-MM-dd, Enter = working date): ", today);
        if (start is null) return;
        var months = ReadInt("Months (1-12): ");
        if (months is null) return;
        var rule = ChooseDiscountRule();

        var result = Pipeline.BuySubscription(clients, subs, clientId.Value, plan.Value, start.Value, months.Value, rule);
        if (result.IsSuccess)
        {
            subs = result.Value.Subscriptions;
            var inv = result.Value.Invoice;
            Console.WriteLine("--- Invoice ---");
            Console.WriteLine($"Subscription #{inv.SubscriptionId}, client {inv.ClientName}, plan {inv.Plan}");
            Console.WriteLine($"Base price: {inv.BasePrice.ToMoney()}");
            Console.WriteLine($"Discount:   {inv.DiscountPercent}%");
            Console.WriteLine($"To pay:     {inv.FinalPrice.ToMoney()}");
        }
        else
        {
            Console.WriteLine("Purchase failed: " + result.Error);
        }
    }

    static void RecordVisitAction()
    {
        var clientId = ReadInt("Client id: ");
        if (clientId is null) return;

        var result = Pipeline.RecordVisit(clients, subs, clientId.Value, today);
        if (result.IsSuccess)
        {
            clients = result.Value.Clients;
            subs = result.Value.Subscriptions;
            Console.WriteLine("Visit recorded.");
        }
        else
        {
            Console.WriteLine("Visit rejected: " + result.Error);
        }
    }

    static void RenewAction()
    {
        var subId = ReadInt("Subscription id: ");
        if (subId is null) return;
        var months = ReadInt("Months to add (1-12): ");
        if (months is null) return;

        var result = Pipeline.RenewSubscription(subs, subId.Value, months.Value, today);
        if (result.IsSuccess)
        {
            subs = result.Value;
            var renewed = subs.First(s => s.Id == subId.Value);
            Console.WriteLine($"Renewed. New end date: {renewed.EndDate:yyyy-MM-dd}");
        }
        else
        {
            Console.WriteLine("Renewal failed: " + result.Error);
        }
    }

    // ---------- Reports ----------

    static void ShowRevenueReport()
    {
        Console.WriteLine("=== Revenue by plan (list price, before discounts) ===");
        var lines = Reports.RevenueLines(Reports.RevenueByPlan(subs));
        if (lines.Count == 0) Console.WriteLine("No data.");
        foreach (var line in lines) Console.WriteLine(line);
    }

    static void ShowExpiringReport()
    {
        var days = ReadInt("Show subscriptions ending within how many days? ");
        if (days is null) return;

        Console.WriteLine($"=== Expiring within {days} days ===");
        var lines = Reports.ExpiringLines(Reports.ExpiringSoon(clients, subs, today, days.Value));
        if (lines.Count == 0) Console.WriteLine("Nothing is expiring.");
        foreach (var line in lines) Console.WriteLine(line);
    }

    static void ListClients()
    {
        foreach (var client in clients.OrderBy(c => c.Id))
        {
            Console.WriteLine($"#{client.Id} {client.Name} ({client.Email}), visits: {client.TotalVisits}{(client.IsStudent ? ", student" : "")}");
            foreach (var sub in subs.Where(s => s.ClientId == client.Id))
            {
                var status = FitnessLogic.GetStatus(sub, today);
                Console.WriteLine($"     sub #{sub.Id} {sub.Plan}, {sub.StartDate:yyyy-MM-dd} to {sub.EndDate:yyyy-MM-dd}, visits {sub.VisitsUsed}, {status}");
            }
        }
    }

    // ---------- Lazy processing demo (deferred execution) ----------

    static void LazyDemo()
    {
        var demoList = subs.ToList();

        // 1. Only describes the query. Nothing is executed yet.
        var query = Reports.StreamExpiring(demoList, today, 10);

        // 2. We add data AFTER creating the query...
        demoList.Add(new Subscription(999, 1, PlanType.Basic, today.AddDays(-5), today.AddDays(2), 0));

        // 3. ...and the query sees it, because it runs only now (deferred execution).
        Console.WriteLine("Expiring when enumerated now: " + query.Count());

        // 4. Deliberate boundary: ToList() makes a snapshot that does not change anymore.
        var snapshot = query.ToList();
        demoList.Add(new Subscription(1000, 2, PlanType.Basic, today.AddDays(-5), today.AddDays(3), 0));

        Console.WriteLine("Snapshot count (after adding one more): " + snapshot.Count);
        Console.WriteLine("Live query count (after adding one more): " + query.Count());
    }

    // ---------- Input helpers (boundary) ----------

    static void ChangeDate()
    {
        var date = ReadDate("New working date (yyyy-MM-dd): ", today);
        if (date is not null)
        {
            today = date.Value;
            Console.WriteLine("Working date changed.");
        }
    }

    static int? ReadInt(string prompt)
    {
        Console.Write(prompt);
        if (int.TryParse(Console.ReadLine(), out var number))
            return number;

        Console.WriteLine("Please enter a whole number.");
        return null;
    }

    static DateTime? ReadDate(string prompt, DateTime defaultDate)
    {
        Console.Write(prompt);
        var text = Console.ReadLine()?.Trim() ?? "";
        if (text == "")
            return defaultDate;
        if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        Console.WriteLine("Date must look like 2026-11-07.");
        return null;
    }

    static PlanType? ReadPlan()
    {
        Console.Write("Plan (Basic / Standard / Premium): ");
        var text = Console.ReadLine()?.Trim() ?? "";
        if (Enum.TryParse<PlanType>(text, true, out var plan) && Enum.IsDefined(plan))
            return plan;

        Console.WriteLine("Unknown plan.");
        return null;
    }

    // The chosen rule is passed into the pipeline as a Func<Client, decimal>
    static Func<Client, decimal> ChooseDiscountRule()
    {
        Console.WriteLine("Discount rule: 1 = none, 2 = student, 3 = loyalty, 4 = best of student/loyalty");
        Console.Write("Choose: ");
        switch (Console.ReadLine()?.Trim())
        {
            case "2": return FitnessLogic.StudentDiscount;
            case "3": return FitnessLogic.LoyaltyDiscount;
            case "4": return FitnessLogic.BestDiscount;
            default: return FitnessLogic.NoDiscount;
        }
    }
}
