using System.Text.Json;
using System.Text.Json.Serialization;

namespace FitnessCrm;

// System boundary: all file access is here, never inside the pure functions.
public static class FileStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static Result<List<Client>> LoadClients(string path)
    {
        return LoadList<Client>(path, FitnessLogic.ValidateClient);
    }

    public static Result<List<Subscription>> LoadSubscriptions(string path)
    {
        return LoadList<Subscription>(path, FitnessLogic.ValidateSubscription);
    }

    public static Result<string> SaveClients(string path, IEnumerable<Client> clients)
    {
        return SaveList(path, clients);
    }

    public static Result<string> SaveSubscriptions(string path, IEnumerable<Subscription> subs)
    {
        return SaveList(path, subs);
    }

    public static Result<string> ExportLines(string path, IEnumerable<string> lines)
    {
        try
        {
            CreateFolderFor(path);
            File.WriteAllLines(path, lines);
            return Result<string>.Ok($"Report exported to {path}");
        }
        catch (IOException ex)
        {
            return Result<string>.Fail($"Cannot write file {path}: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<string>.Fail($"No permission to write {path}: {ex.Message}");
        }
    }

    // ---------- helpers ----------

    private static Result<List<T>> LoadList<T>(string path, Func<T, Result<T>> validateItem)
    {
        try
        {
            if (!File.Exists(path))
                return Result<List<T>>.Fail($"File not found: {path}");

            var json = File.ReadAllText(path);
            var items = JsonSerializer.Deserialize<List<T>>(json, Options);
            if (items is null)
                return Result<List<T>>.Fail($"File has no data: {path}");

            // validate every loaded item
            foreach (var item in items)
            {
                if (item is null)
                    return Result<List<T>>.Fail($"File contains an empty entry: {path}");

                var check = validateItem(item);
                if (!check.IsSuccess)
                    return Result<List<T>>.Fail($"Invalid data in {Path.GetFileName(path)}: {check.Error}");
            }

            return Result<List<T>>.Ok(items);
        }
        catch (JsonException ex)
        {
            return Result<List<T>>.Fail($"Malformed JSON in {path}: {ex.Message}");
        }
        catch (IOException ex)
        {
            return Result<List<T>>.Fail($"Cannot read file {path}: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<List<T>>.Fail($"No permission to read {path}: {ex.Message}");
        }
    }

    private static Result<string> SaveList<T>(string path, IEnumerable<T> items)
    {
        try
        {
            CreateFolderFor(path);
            var json = JsonSerializer.Serialize(items.ToList(), Options);
            File.WriteAllText(path, json);
            return Result<string>.Ok($"Saved to {path}");
        }
        catch (IOException ex)
        {
            return Result<string>.Fail($"Cannot write file {path}: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<string>.Fail($"No permission to write {path}: {ex.Message}");
        }
    }

    private static void CreateFolderFor(string path)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);
    }
}
