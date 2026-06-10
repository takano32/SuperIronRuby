namespace SuperIronRuby.Hosting;

/// <summary>
/// Public embedding API, shaped like IronRuby's <c>Ruby.CreateEngine()</c>.
/// Wired to the runtime/interpreter in the integration phase; see Docs/CONTRACTS.md.
/// </summary>
public static class Ruby
{
    public static ScriptEngine CreateEngine() => new ScriptEngine();
}

public sealed class ScriptEngine
{
    public object? Execute(string code)
        => throw new NotImplementedException("SuperIronRuby engine is not wired yet (integration phase).");

    public object? ExecuteFile(string path)
        => throw new NotImplementedException("SuperIronRuby engine is not wired yet (integration phase).");

    /// <summary>Executes <paramref name="code"/> capturing standard output; used by tests.</summary>
    public string ExecuteToString(string code)
        => throw new NotImplementedException("SuperIronRuby engine is not wired yet (integration phase).");
}
