namespace SuperIronRuby.Parser;

/// <summary>The result of parsing Ruby source with Prism.</summary>
public sealed class ParseResult
{
    /// <summary>The root program node.</summary>
    public ProgramNode Root { get; }

    /// <summary>The original source text.</summary>
    public string Source { get; }

    /// <summary>The file path (or "(eval)").</summary>
    public string FilePath { get; }

    public IReadOnlyList<ParseDiagnostic> Errors { get; }
    public IReadOnlyList<ParseDiagnostic> Warnings { get; }

    private readonly Loader _loader;

    internal ParseResult(ProgramNode root, string source, string filePath, Loader loader)
    {
        Root = root;
        Source = source;
        FilePath = filePath;
        _loader = loader;
        Errors = loader.Errors;
        Warnings = loader.Warnings;
    }

    /// <summary>True when there were no syntax errors.</summary>
    public bool Success => Errors.Count == 0;

    /// <summary>1-based line number for a byte offset into the source.</summary>
    public int LineForOffset(int byteOffset) => _loader.LineForOffset(byteOffset);
}
