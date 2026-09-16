using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TranscriptMvp;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private static readonly Dictionary<string, string> DemoHashes = new()
    {
        ["1"] = "c89b2b840b331e74dacf4fc2570db47346c9388586bb6c6c78efda0a59fcc9a5",
        ["2"] = "5a364bb0d6ae9ede7aeef20791c329d95222b3e3fe37f9c290c561b5756816b9",
        ["3"] = "b97dedbb96a5d071439ca498dd962f213991b8ec5bc0546c54105d89f4a2a696",
        ["4"] = "5a75282b9ef2cf6b1636f0bc75d75e741fbf3381cd470296cbd68c6f101ae0ef"
    };

    private static async Task<int> Main(string[] args)
    {
        try
        {
            var root = FindRoot();
            var options = CommandLineOptions.Parse(args, root);
            if (options.Help)
            {
                Console.WriteLine(CommandLineOptions.Usage);
                return 0;
            }

            var files = FindTranscripts(options.InputPath);
            var transcripts = files.ToDictionary(path => Path.GetFileNameWithoutExtension(path)!, File.ReadAllText);
            var batch = options.Demo
                ? await LoadDemoAsync(root, files)
                : await AnalyzeAsync(transcripts);

            batch.GeneratedAt = DateTimeOffset.UtcNow.ToString("O");
            ResultValidator.Validate(batch, transcripts);
            await WriteResultsAsync(batch, transcripts, options.OutputPath);
            Console.WriteLine($"Validated {batch.Conversations.Count} transcript(s). Wrote {Path.Combine(options.OutputPath, "results.json")} and {Path.Combine(options.OutputPath, "report.html")}.");
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            return 1;
        }
    }

    private static string[] FindTranscripts(string inputPath)
    {
        var files = Directory.Exists(inputPath)
            ? Directory.GetFiles(inputPath, "*.txt").OrderBy(path => path, StringComparer.Ordinal).ToArray()
            : File.Exists(inputPath) ? [inputPath] : throw new FileNotFoundException("Input path does not exist.", inputPath);
        if (files.Length == 0) throw new InvalidDataException("No .txt transcripts found.");
        return files;
    }

    private static async Task<ResultBatch> LoadDemoAsync(string root, string[] files)
    {
        VerifyDemoInputs(files);
        var path = Path.Combine(root, "data", "demo-results.json");
        var batch = JsonSerializer.Deserialize<ResultBatch>(await File.ReadAllTextAsync(path), Json)
            ?? throw new InvalidDataException("Demo results are empty.");
        batch.Mode = "demo (manually verified, no model call)";
        return batch;
    }

    private static async Task<ResultBatch> AnalyzeAsync(Dictionary<string, string> transcripts)
    {
        var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("OPENAI_API_KEY is absent. Run with --demo for the supplied case materials.");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        var analyzer = new OpenAiAnalyzer(
            http,
            key,
            Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "https://api.openai.com/v1",
            Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini");
        var batch = new ResultBatch
        {
            Mode = "api",
            GeneratedAt = DateTimeOffset.UtcNow.ToString("O"),
            Conversations = []
        };
        foreach (var (id, source) in transcripts.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"Analyzing transcript {id}...");
            batch.Conversations.Add(await analyzer.AnalyzeAsync(id, source));
        }
        return batch;
    }

    private static async Task WriteResultsAsync(ResultBatch batch, Dictionary<string, string> transcripts, string outputPath)
    {
        Directory.CreateDirectory(outputPath);
        var jsonPath = Path.Combine(outputPath, "results.json");
        var htmlPath = Path.Combine(outputPath, "report.html");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(batch, Json), new UTF8Encoding(false));
        await File.WriteAllTextAsync(htmlPath, ReportBuilder.Build(batch), new UTF8Encoding(false));
        var restored = JsonSerializer.Deserialize<ResultBatch>(await File.ReadAllTextAsync(jsonPath), Json)
            ?? throw new InvalidDataException("Could not deserialize results.json.");
        ResultValidator.Validate(restored, transcripts);
    }

    private static void VerifyDemoInputs(string[] files)
    {
        if (files.Length != DemoHashes.Count) throw new InvalidDataException("Demo mode requires the four supplied transcripts.");
        foreach (var file in files)
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (!DemoHashes.TryGetValue(id, out var expected))
                throw new InvalidDataException($"Unknown demo transcript: {id}.");
            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
            if (actual != expected) throw new InvalidDataException($"Demo transcript {id} differs from the verified case material.");
        }
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "data", "demo-results.json"))) return directory.FullName;
        throw new DirectoryNotFoundException("Run from the project directory or one of its children.");
    }
}
