using Xunit;

namespace FitnessCrm.Tests;

// Tests for the file boundary. Each test uses its own temporary folder.
public class FileTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "fitness_tests_" + Guid.NewGuid());

    public FileTests()
    {
        Directory.CreateDirectory(folder);
    }

    public void Dispose()
    {
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
    }

    [Fact]
    public void LoadClients_MissingFile_ReturnsFailure()
    {
        var path = Path.Combine(folder, "does_not_exist.json");

        var result = FileStorage.LoadClients(path);

        Assert.False(result.IsSuccess);
        Assert.Contains("File not found", result.Error);
    }

    [Fact]
    public void LoadClients_MalformedJson_ReturnsFailure()
    {
        var path = Path.Combine(folder, "broken.json");
        File.WriteAllText(path, "[ { \"Id\": 1, \"Name\": ");   // cut in the middle

        var result = FileStorage.LoadClients(path);

        Assert.False(result.IsSuccess);
        Assert.Contains("Malformed JSON", result.Error);
    }

    [Fact]
    public void LoadClients_ValidJsonButInvalidData_ReturnsFailure()
    {
        var path = Path.Combine(folder, "bad_data.json");
        File.WriteAllText(path,
            "[ { \"Id\": 1, \"Name\": \"\", \"Email\": \"a@example.com\", \"Age\": 20, \"IsStudent\": false, \"TotalVisits\": 0 } ]");

        var result = FileStorage.LoadClients(path);

        Assert.False(result.IsSuccess);
        Assert.Contains("name is required", result.Error);
    }

    [Fact]
    public void SaveThenLoad_Subscriptions_KeepsData()
    {
        var path = Path.Combine(folder, "nested", "subs.json");   // folder is created by Save
        var subs = new List<Subscription>
        {
            new(1, 1, PlanType.Premium, new DateTime(2026, 11, 1), new DateTime(2026, 12, 1), 3)
        };

        var saveResult = FileStorage.SaveSubscriptions(path, subs);
        var loadResult = FileStorage.LoadSubscriptions(path);

        Assert.True(saveResult.IsSuccess);
        Assert.True(loadResult.IsSuccess);
        Assert.Single(loadResult.Value);
        Assert.Equal(PlanType.Premium, loadResult.Value[0].Plan);
        Assert.Equal(5, loadResult.Value[0].VisitsUsed);
    }
}
