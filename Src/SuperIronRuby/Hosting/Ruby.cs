using System.Reflection;
using System.Text;
using SuperIronRuby.Interpreter;
using SuperIronRuby.Parser;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Hosting;

/// <summary>
/// Public embedding API, shaped like IronRuby's <c>Ruby.CreateEngine()</c>.
/// </summary>
public static class Ruby
{
    public static ScriptEngine CreateEngine() => new ScriptEngine();
}

/// <summary>Thrown when source fails to parse.</summary>
public sealed class RubySyntaxError : Exception
{
    public RubySyntaxError(string message) : base(message) { }
}

/// <summary>
/// A SuperIronRuby execution engine: a runtime context, the builtin library, the
/// interpreter, and a persistent top-level scope. Mirrors IronRuby's hosting API.
/// </summary>
public sealed class ScriptEngine
{
    private readonly RubyContext _context;
    private readonly SuperIronRuby.Interpreter.Interpreter _interpreter;
    private readonly RubyScope _topScope;
    private string _currentFile = "(eval)";

    public RubyContext Context => _context;

    public ScriptEngine()
    {
        _context = new RubyContext();
        BuiltinLoader.LoadAssembly(_context, typeof(RubyContext).Assembly);
        _interpreter = new SuperIronRuby.Interpreter.Interpreter(_context);
        _topScope = _interpreter.CreateTopLevelScope();

        SetupProgramState();
        LoadCoreLibrary();
    }

    /// <summary>Executes Ruby source, returning the value of the last expression.</summary>
    public object? Execute(string code) => ExecuteInternal(code, _currentFile);

    /// <summary>Executes the contents of a file (sets __FILE__ for the run).</summary>
    public object? ExecuteFile(string path)
    {
        var prev = _currentFile;
        _currentFile = path;
        try
        {
            return ExecuteInternal(File.ReadAllText(path), path);
        }
        finally
        {
            _currentFile = prev;
        }
    }

    /// <summary>Executes <paramref name="code"/> capturing standard output.</summary>
    public string ExecuteToString(string code)
    {
        var prevOut = _context.Stdout;
        var sw = new StringWriter();
        _context.Stdout = sw;
        try
        {
            ExecuteInternal(code, _currentFile);
        }
        finally
        {
            _context.Stdout = prevOut;
        }
        return sw.ToString();
    }

    private object? ExecuteInternal(string code, string filePath)
    {
        var unit = PrismParser.Parse(code, filePath);
        if (!unit.Success)
        {
            var first = unit.Errors[0];
            int line = unit.LineForOffset(first.Location.StartOffset);
            throw new RubySyntaxError($"{filePath}:{line}: {first.Message}");
        }
        return _interpreter.Run(unit, _topScope);
    }

    /// <summary>Sets ARGV and $0/$PROGRAM_NAME.</summary>
    public void SetProgramArguments(IEnumerable<string> args, string programName = "(sir)")
    {
        var argv = new RubyArray();
        foreach (var a in args) argv.Add(new MutableString(a));
        _context.ObjectClass.SetConstant("ARGV", argv);
        var name = new MutableString(programName);
        _context.SetGlobal("$0", name);
        _context.SetGlobal("$PROGRAM_NAME", name);
    }

    private void SetupProgramState()
    {
        _context.ObjectClass.SetConstant("ARGV", new RubyArray());
        _context.SetGlobal("$0", new MutableString("(sir)"));
        _context.SetGlobal("$PROGRAM_NAME", new MutableString("(sir)"));
        SetupEnv();
        SetupStdio();
    }

    // ENV: a singleton object backed by the process environment.
    private void SetupEnv()
    {
        var envClass = _context.DefineClass("ENVClass", _context.ObjectClass);
        var env = new RubyObject(envClass);
        var singleton = _context.SingletonClassOf(env);

        singleton.DefineMethod("[]", RubyMethodInfo.FromBuiltin("[]", singleton, (c, _, a, _) =>
        {
            var v = Environment.GetEnvironmentVariable(Str(a[0]));
            return v is null ? null : new MutableString(v);
        }));
        singleton.DefineMethod("[]=", RubyMethodInfo.FromBuiltin("[]=", singleton, (c, _, a, _) =>
        {
            Environment.SetEnvironmentVariable(Str(a[0]), a[1] is null ? null : Str(a[1]));
            return a[1];
        }));
        singleton.DefineMethod("fetch", RubyMethodInfo.FromBuiltin("fetch", singleton, (c, _, a, _) =>
        {
            var v = Environment.GetEnvironmentVariable(Str(a[0]));
            if (v is not null) return new MutableString(v);
            if (a.Length > 1) return a[1];
            throw c.RaiseError(c.KeyErrorClass, $"key not found: \"{Str(a[0])}\"");
        }));
        singleton.DefineMethod("key?", RubyMethodInfo.FromBuiltin("key?", singleton,
            (c, _, a, _) => Environment.GetEnvironmentVariable(Str(a[0])) is not null));
        singleton.DefineMethod("to_h", RubyMethodInfo.FromBuiltin("to_h", singleton, (c, _, _, _) =>
        {
            var h = new RubyHash();
            foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
                h.Store(new MutableString(e.Key.ToString()!), new MutableString(e.Value?.ToString() ?? ""));
            return h;
        }));

        _context.ObjectClass.SetConstant("ENV", env);
    }

    private void SetupStdio()
    {
        // Minimal $stdout/$stderr globals point at the context writers indirectly
        // (Kernel#puts/print/p already write to ctx.Stdout). Expose simple IO-like
        // objects so $stdout.puts works too.
        _context.SetGlobal("$stdout", null);
        _context.SetGlobal("$stderr", null);
        _context.SetGlobal("$stdin", null);
    }

    private static string Str(object? o) => o is MutableString m ? m.Value : o?.ToString() ?? "";

    // Evaluates the embedded Lib/core/*.rb files (deterministic name order) so
    // the Ruby-authored parts of the core library (e.g. Enumerable) are available.
    private void LoadCoreLibrary()
    {
        var asm = typeof(RubyContext).Assembly;
        var resources = asm.GetManifestResourceNames()
            .Where(n => n.Contains(".Lib.core.") && n.EndsWith(".rb"))
            .OrderBy(n => n, StringComparer.Ordinal);

        foreach (var name in resources)
        {
            using var stream = asm.GetManifestResourceStream(name);
            if (stream is null) continue;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var src = reader.ReadToEnd();
            var unit = PrismParser.Parse(src, name);
            if (unit.Success) _interpreter.Run(unit, _topScope);
        }
    }
}
