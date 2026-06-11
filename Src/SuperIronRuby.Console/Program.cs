using SuperIronRuby.Hosting;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Cli;

/// <summary>The <c>sir</c> command-line front end for SuperIronRuby.</summary>
internal static class Program
{
    private const string Version = "SuperIronRuby 0.1.0 (Ruby 4.0 target) [.NET 10]";

    public static int Main(string[] args)
    {
        var eval = new List<string>();
        string? file = null;
        var scriptArgs = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (file is not null) { scriptArgs.Add(a); continue; }

            switch (a)
            {
                case "--version" or "-v":
                    Console.WriteLine(Version);
                    return 0;
                case "--help" or "-h":
                    PrintUsage();
                    return 0;
                case "-e":
                    if (++i >= args.Length) { Console.Error.WriteLine("sir: -e requires an argument"); return 1; }
                    eval.Add(args[i]);
                    break;
                case "--ast":
                    if (++i >= args.Length) { Console.Error.WriteLine("sir: --ast requires a file"); return 1; }
                    return DumpAst(args[i]);
                default:
                    if (a.StartsWith('-') && a.Length > 1)
                    {
                        Console.Error.WriteLine($"sir: unknown option {a}");
                        return 1;
                    }
                    file = a;
                    break;
            }
        }

        var engine = Ruby.CreateEngine();

        if (eval.Count > 0)
        {
            engine.SetProgramArguments(scriptArgs, "-e");
            return RunGuarded(() => engine.Execute(string.Join('\n', eval)), engine);
        }

        if (file is not null)
        {
            engine.SetProgramArguments(scriptArgs, file);
            return RunGuarded(() => engine.ExecuteFile(file), engine);
        }

        return Repl(engine);
    }

    // Runs an action, formatting uncaught Ruby exceptions like MRI and mapping
    // SystemExit to its status code.
    private static int RunGuarded(Action action, ScriptEngine engine)
    {
        try
        {
            action();
            return 0;
        }
        catch (RubySyntaxError ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (RubyRaiseException ex)
        {
            var rex = ex.RubyException;
            if (rex.RubyClass == engine.Context.SystemExitClass)
                return 0;   // SystemExit: status handling refined in B13
            var className = rex.RubyClass.Name ?? "Exception";
            var message = string.IsNullOrEmpty(rex.Message) ? className : rex.Message;
            Console.Error.WriteLine($"{message} ({className})");
            return 1;
        }
        catch (NotSupportedException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Repl(ScriptEngine engine)
    {
        Console.WriteLine(Version);
        int line = 1;
        var buffer = new System.Text.StringBuilder();

        while (true)
        {
            Console.Write(buffer.Length == 0 ? $"sir({line:000})> " : "          ... ");
            var input = Console.ReadLine();
            if (input is null) { Console.WriteLine(); break; }   // Ctrl-D

            buffer.AppendLine(input);
            var code = buffer.ToString();

            // Continue reading on a recoverable "unexpected end-of-input" error.
            if (NeedsMoreInput(code))
                continue;

            buffer.Clear();
            line++;
            try
            {
                var result = engine.Execute(code);
                Console.WriteLine("=> " + engine.Context.Inspect(result));
            }
            catch (RubySyntaxError ex) { Console.Error.WriteLine(ex.Message); }
            catch (RubyRaiseException ex)
            {
                var rex = ex.RubyException;
                var msg = string.IsNullOrEmpty(rex.Message) ? rex.RubyClass.Name : rex.Message;
                Console.Error.WriteLine($"{msg} ({rex.RubyClass.Name})");
            }
            catch (NotSupportedException ex) { Console.Error.WriteLine(ex.Message); }
        }
        return 0;
    }

    private static bool NeedsMoreInput(string code)
    {
        var result = SuperIronRuby.Parser.PrismParser.Parse(code);
        if (result.Success) return false;
        foreach (var e in result.Errors)
            if (e.Message.Contains("unexpected end-of-input") || e.Message.Contains("unexpected end-of-file"))
                return true;
        return false;
    }

    private static int DumpAst(string file)
    {
        var src = File.ReadAllText(file);
        var result = SuperIronRuby.Parser.PrismParser.Parse(src, file);
        DumpNode(result.Root, 0);
        Console.WriteLine($"errors: {result.Errors.Count}");
        return 0;
    }

    private static void DumpNode(SuperIronRuby.Parser.Node? node, int depth)
    {
        if (node is null) return;
        Console.WriteLine($"{new string(' ', depth * 2)}{node.Type} {node.Location}");
        foreach (var child in node.ChildNodes())
            if (child is not null) DumpNode(child, depth + 1);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: sir [options] [file] [args...]");
        Console.WriteLine("  -e CODE       execute CODE (may be repeated)");
        Console.WriteLine("  --ast FILE    print the parsed AST of FILE");
        Console.WriteLine("  -v, --version print version");
        Console.WriteLine("  (no file)     start a REPL");
    }
}
