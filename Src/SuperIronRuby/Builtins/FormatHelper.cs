using System.Globalization;
using System.Numerics;
using System.Text;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Builtins;

/// <summary>A subset of Ruby's <c>sprintf</c>/<c>format</c> directives, shared by
/// Kernel#format/sprintf (B1) and String#% (B3).</summary>
public static class FormatHelper
{
    public static string Sprintf(RubyContext ctx, string format, object?[] args)
    {
        var sb = new StringBuilder();
        int argi = 0;
        for (int i = 0; i < format.Length; i++)
        {
            char ch = format[i];
            if (ch != '%') { sb.Append(ch); continue; }

            int start = ++i;
            if (i < format.Length && format[i] == '%') { sb.Append('%'); continue; }

            // flags
            string flags = "";
            while (i < format.Length && "-+ 0#".IndexOf(format[i]) >= 0) flags += format[i++];
            // width
            string width = "";
            while (i < format.Length && char.IsDigit(format[i])) width += format[i++];
            // precision
            string prec = "";
            if (i < format.Length && format[i] == '.')
            {
                i++;
                while (i < format.Length && char.IsDigit(format[i])) prec += format[i++];
            }
            if (i >= format.Length) { sb.Append(format[start..]); break; }

            char conv = format[i];
            object? arg = argi < args.Length ? args[argi++] : null;
            sb.Append(FormatOne(ctx, conv, flags, width, prec, arg));
        }
        return sb.ToString();
    }

    private static string FormatOne(RubyContext ctx, char conv, string flags, string width, string prec, object? arg)
    {
        string body = conv switch
        {
            'd' or 'i' or 'u' => ToBig(arg).ToString(CultureInfo.InvariantCulture),
            'f' => ToDouble(arg).ToString("F" + (prec == "" ? "6" : prec), CultureInfo.InvariantCulture),
            'e' => ToDouble(arg).ToString("0." + new string('0', prec == "" ? 6 : int.Parse(prec)) + "e+00", CultureInfo.InvariantCulture),
            'g' => ToDouble(arg).ToString("G" + (prec == "" ? "" : prec), CultureInfo.InvariantCulture),
            'x' => ToHex(ToBig(arg), false),
            'X' => ToHex(ToBig(arg), true),
            'o' => ToBase(ToBig(arg), 8),
            'b' => ToBase(ToBig(arg), 2),
            's' => ctx.ToStr(arg),
            'c' => arg is long l ? ((char)l).ToString() : ctx.ToStr(arg),
            _ => "%" + conv,
        };

        // sign flag for numbers
        if ((conv is 'd' or 'i' or 'f' or 'e' or 'g') && !body.StartsWith('-'))
        {
            if (flags.Contains('+')) body = "+" + body;
            else if (flags.Contains(' ')) body = " " + body;
        }

        // precision for integers/strings used as min-digits/max-length
        if (conv == 's' && prec != "") { int p = int.Parse(prec); if (body.Length > p) body = body[..p]; }

        // width + padding
        if (width != "")
        {
            int w = int.Parse(width);
            if (body.Length < w)
            {
                if (flags.Contains('-')) body = body.PadRight(w);
                else if (flags.Contains('0') && conv != 's')
                {
                    bool neg = body.StartsWith('-') || body.StartsWith('+');
                    string sign = neg ? body[..1] : "";
                    string digits = neg ? body[1..] : body;
                    body = sign + digits.PadLeft(w - sign.Length, '0');
                }
                else body = body.PadLeft(w);
            }
        }
        return body;
    }

    private static BigInteger ToBig(object? o) => o switch
    {
        long l => l, BigInteger b => b, double d => (BigInteger)d,
        MutableString s => BigInteger.Parse(s.Value), _ => 0,
    };

    private static double ToDouble(object? o) => o switch
    {
        long l => l, BigInteger b => (double)b, double d => d, _ => 0,
    };

    private static string ToHex(BigInteger v, bool upper)
    {
        var s = (v < 0 ? -v : v).ToString(upper ? "X" : "x").TrimStart('0');
        if (s == "") s = "0";
        if (upper) s = s.ToUpperInvariant(); else s = s.ToLowerInvariant();
        return (v < 0 ? "-" : "") + s;
    }

    private static string ToBase(BigInteger v, int radix)
    {
        if (v == 0) return "0";
        bool neg = v < 0; v = BigInteger.Abs(v);
        const string digits = "0123456789abcdef";
        var sb = new StringBuilder();
        while (v > 0) { sb.Insert(0, digits[(int)(v % radix)]); v /= radix; }
        return (neg ? "-" : "") + sb;
    }
}

[RubyModule("Kernel")]
public static class FormatKernelOps
{
    [RubyMethod("format", Kind = RubyMethodKind.ModuleFunction)]
    [RubyMethod("sprintf", Kind = RubyMethodKind.ModuleFunction)]
    public static object? Format(RubyContext c, object? self, object?[] a, RubyProc? b)
        => new MutableString(FormatHelper.Sprintf(c, a[0] is MutableString m ? m.Value : "", a.Skip(1).ToArray()));
}
