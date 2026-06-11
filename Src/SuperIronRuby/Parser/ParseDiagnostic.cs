namespace SuperIronRuby.Parser;

/// <summary>Severity of a parse diagnostic.</summary>
public enum DiagnosticSeverity
{
    Error,
    Warning,
}

/// <summary>A parse error or warning produced by Prism.</summary>
public sealed class ParseDiagnostic
{
    public DiagnosticSeverity Severity { get; }

    /// <summary>The diagnostic message text.</summary>
    public string Message { get; }

    /// <summary>Source location the diagnostic refers to.</summary>
    public Location Location { get; }

    /// <summary>Prism's diagnostic-type id (index into its DIAGNOSTIC_TYPES table).</summary>
    public int TypeId { get; }

    /// <summary>Prism's level byte (error: 0=syntax,1=argument,2=load;
    /// warning: 0=default,1=verbose).</summary>
    public int Level { get; }

    public ParseDiagnostic(DiagnosticSeverity severity, int typeId, string message, Location location, int level)
    {
        Severity = severity;
        TypeId = typeId;
        Message = message;
        Location = location;
        Level = level;
    }

    public override string ToString() => $"{Severity}: {Message} {Location}";
}
