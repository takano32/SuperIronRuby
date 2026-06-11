using System.Diagnostics;
using System.Text;

namespace SuperIronRuby.Parser;

/// <summary>
/// The public entry point for parsing Ruby source into a Prism AST. By default
/// it parses in-process via libprism (P/Invoke); set the environment variable
/// <c>SIR_PARSER_BACKEND=mri</c> to shell out to the system <c>ruby</c>'s
/// <c>Prism.dump</c> instead (useful where the native library is unavailable).
/// </summary>
public static class PrismParser
{
    /// <summary>Parses Ruby <paramref name="source"/>. Syntax errors are reported
    /// via <see cref="ParseResult.Errors"/>, not exceptions.</summary>
    public static ParseResult Parse(string source, string filePath = "(eval)")
    {
        var sourceBytes = Encoding.UTF8.GetBytes(source);
        var serialized = SerializeWithBackend(sourceBytes);

        var loader = new Loader(serialized, sourceBytes);
        var root = loader.LoadProgram();
        return new ParseResult(root, source, filePath, loader);
    }

    private static byte[] SerializeWithBackend(byte[] sourceBytes)
    {
        var backend = Environment.GetEnvironmentVariable("SIR_PARSER_BACKEND");
        return backend == "mri" ? SerializeViaMri(sourceBytes) : PrismNative.SerializeParse(sourceBytes);
    }

    // Fallback backend: pipe the source through `ruby -rprism -e 'print Prism.dump(STDIN.read)'`.
    private static byte[] SerializeViaMri(byte[] sourceBytes)
    {
        var psi = new ProcessStartInfo("ruby")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("--disable-gems");
        psi.ArgumentList.Add("-rprism");
        psi.ArgumentList.Add("-e");
        psi.ArgumentList.Add("STDOUT.binmode; STDIN.binmode; print Prism.dump(STDIN.read)");

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("failed to start ruby for the MRI parser backend");

        using (var stdin = proc.StandardInput.BaseStream)
            stdin.Write(sourceBytes, 0, sourceBytes.Length);

        using var ms = new MemoryStream();
        proc.StandardOutput.BaseStream.CopyTo(ms);
        proc.WaitForExit();
        return ms.ToArray();
    }
}
