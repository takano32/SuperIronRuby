using System.Text.RegularExpressions;

namespace SuperIronRuby.Runtime;

/// <summary>
/// Ruby option flags for a <see cref="RubyRegexp"/>. Values match the bits Ruby
/// exposes via <c>Regexp::IGNORECASE</c> (1), <c>EXTENDED</c> (2),
/// <c>MULTILINE</c> (4).
/// </summary>
[Flags]
public enum RubyRegexpOptions
{
    None = 0,
    /// <summary><c>/i</c> — case-insensitive.</summary>
    IgnoreCase = 1,
    /// <summary><c>/x</c> — extended (ignore pattern whitespace/comments).</summary>
    Extended = 2,
    /// <summary><c>/m</c> — Ruby "multiline": dot matches newline.</summary>
    Multiline = 4,
}

/// <summary>
/// Ruby's <c>Regexp</c>. Holds the original source text and Ruby option flags;
/// the underlying .NET <see cref="Regex"/> is built lazily on first use.
/// </summary>
/// <remarks>
/// Option mapping (note the semantic mismatch between Ruby and .NET): Ruby's
/// <c>/m</c> means "dot matches newline", which is .NET
/// <see cref="RegexOptions.Singleline"/> — NOT .NET's <c>Multiline</c>. Ruby's
/// <c>^</c>/<c>$</c> are always line anchors, so .NET
/// <see cref="RegexOptions.Multiline"/> is applied unconditionally. <c>/x</c>
/// maps to <see cref="RegexOptions.IgnorePatternWhitespace"/> and <c>/i</c> to
/// <see cref="RegexOptions.IgnoreCase"/>.
/// </remarks>
public sealed class RubyRegexp
{
    private Regex? _compiled;

    /// <summary>The original Ruby pattern source.</summary>
    public string Source { get; }

    /// <summary>The Ruby option flags this regexp was created with.</summary>
    public RubyRegexpOptions Options { get; }

    public RubyRegexp(string source, RubyRegexpOptions options = RubyRegexpOptions.None)
    {
        Source = source ?? string.Empty;
        Options = options;
    }

    /// <summary>The lazily-built .NET regex (compiled on first access).</summary>
    public Regex Regex => _compiled ??= Build();

    private Regex Build()
    {
        // Ruby ^/$ are always line anchors -> always Multiline.
        var net = RegexOptions.Multiline;

        if ((Options & RubyRegexpOptions.IgnoreCase) != 0)
            net |= RegexOptions.IgnoreCase;
        if ((Options & RubyRegexpOptions.Extended) != 0)
            net |= RegexOptions.IgnorePatternWhitespace;
        // Ruby /m == "dot matches newline" == .NET Singleline (NOT .NET Multiline).
        if ((Options & RubyRegexpOptions.Multiline) != 0)
            net |= RegexOptions.Singleline;

        return new Regex(TranslatePattern(Source), net);
    }

    /// <summary>
    /// Seam for converting Ruby regex syntax to .NET syntax. Minimal for task
    /// R1: most constructs already overlap.
    /// </summary>
    /// <remarks>
    /// Pass-through today (already supported by .NET): <c>\A \z \Z</c> anchors,
    /// named groups <c>(?&lt;name&gt;...)</c>.
    /// TODO (later task) — translate Ruby-only syntax not understood by .NET:
    ///   - POSIX bracket classes <c>[[:alpha:]]</c> etc.
    ///   - <c>\h</c>/<c>\H</c> (hex digit) and other Ruby shorthand classes
    ///   - <c>\G</c> anchor differences
    ///   - <c>(?&lt;name&gt;...)</c> backreferences <c>\k&lt;name&gt;</c> vs .NET <c>\k&lt;name&gt;</c>
    ///   - possessive quantifiers / atomic-group corner cases
    ///   - Unicode property names that differ from .NET's
    /// </remarks>
    public static string TranslatePattern(string pattern) => pattern;
}
