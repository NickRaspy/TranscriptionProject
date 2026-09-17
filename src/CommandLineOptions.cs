namespace TranscriptMvp;

public sealed record CommandLineOptions(bool Demo, bool Gemini, string InputPath, string OutputPath, bool Help)
{
    public const string Usage = "Usage: dotnet run --project src -- [--demo | --gemini] [--input FILE_OR_DIRECTORY] [--output DIRECTORY]";

    public static CommandLineOptions Parse(string[] args, string root)
    {
        var demo = false;
        var gemini = false;
        var help = false;
        string? input = null;
        string? output = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < args.Length; i++)
        {
            var option = args[i];
            if (option is not ("--demo" or "--gemini" or "--help" or "-h" or "--input" or "--output"))
                throw new ArgumentException($"Unknown argument: {option}. {Usage}");
            if (!seen.Add(option)) throw new ArgumentException($"Repeated argument: {option}.");

            switch (option)
            {
                case "--demo":
                    demo = true;
                    break;
                case "--gemini":
                    gemini = true;
                    break;
                case "--help" or "-h":
                    help = true;
                    break;
                case "--input" or "--output":
                    if (++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal))
                        throw new ArgumentException($"Missing value for {option}.");
                    if (option == "--input") input = args[i];
                    else output = args[i];
                    break;
            }
        }

        if (demo && gemini) throw new ArgumentException("--demo and --gemini cannot be used together.");

        return new CommandLineOptions(
            demo,
            gemini,
            Path.GetFullPath(input ?? Path.Combine(root, "data", "transcripts")),
            Path.GetFullPath(output ?? root),
            help);
    }
}
