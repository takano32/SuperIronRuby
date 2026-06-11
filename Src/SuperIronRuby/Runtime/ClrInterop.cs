using System.Reflection;

namespace SuperIronRuby.Runtime;

/// <summary>
/// A Ruby module standing in for a CLR namespace (e.g. <c>System</c>,
/// <c>System::Text</c>). Looking up a constant on it resolves to either a child
/// namespace module or a wrapped CLR type. This is SuperIronRuby's take on
/// IronRuby's <c>System::...</c> interop.
/// </summary>
public sealed class ClrNamespaceModule : RubyModule
{
    public string Namespace { get; }
    private readonly RubyContext _context;

    public ClrNamespaceModule(RubyContext context, string ns)
    {
        _context = context;
        Namespace = ns;
        Name = ns.Replace('.', ':').Replace(":", "::");
    }

    /// <summary>Resolves <paramref name="name"/> under this namespace: a CLR type
    /// (wrapped as a RubyClass) or a child namespace module. Cached as a real
    /// constant after first resolution.</summary>
    public object? Resolve(string name)
    {
        if (TryGetOwnConstant(name, out var cached)) return cached;

        var full = Namespace + "." + name;
        var type = ClrInterop.FindType(full);
        object? result = type is not null
            ? _context.WrapClrType(type)
            : new ClrNamespaceModule(_context, full);

        SetConstant(name, result);
        return result;
    }
}

/// <summary>CLR interop helpers: type lookup, method dispatch via reflection,
/// and value conversion between Ruby and CLR representations.</summary>
public static class ClrInterop
{
    /// <summary>Finds a CLR type by full name across all loaded assemblies.</summary>
    public static Type? FindType(string fullName)
    {
        var t = Type.GetType(fullName);
        if (t is not null) return t;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            t = asm.GetType(fullName);
            if (t is not null) return t;
        }
        return null;
    }

    /// <summary>snake_case → PascalCase for mapping Ruby method names to CLR members.</summary>
    public static string PascalCase(string name)
    {
        if (name.Length == 0) return name;
        var parts = name.Split('_');
        return string.Concat(parts.Select(p => p.Length == 0 ? "" : char.ToUpperInvariant(p[0]) + p[1..]));
    }

    /// <summary>Converts a Ruby value to a CLR value for a target parameter type.</summary>
    public static object? ToClr(object? value, Type target)
    {
        if (value is null) return null;
        if (target.IsInstanceOfType(value)) return value;
        if (value is MutableString s && target == typeof(string)) return s.Value;
        if (value is long l)
        {
            if (target == typeof(int)) return (int)l;
            if (target == typeof(long)) return l;
            if (target == typeof(double)) return (double)l;
            if (target == typeof(float)) return (float)l;
        }
        if (value is double d)
        {
            if (target == typeof(double)) return d;
            if (target == typeof(float)) return (float)d;
        }
        if (value is bool b && target == typeof(bool)) return b;
        return value;
    }

    /// <summary>Converts a CLR return value back to a Ruby value.</summary>
    public static object? FromClr(object? value)
        => value switch
        {
            null => null,
            string str => new MutableString(str),
            int i => (long)i,
            long lng => lng,
            short sh => (long)sh,
            byte by => (long)by,
            float f => (double)f,
            double db => db,
            bool bl => bl,
            _ => value,   // arbitrary CLR object — dispatched via its wrapper class
        };

    /// <summary>Best-effort overload selection: the first method with a matching
    /// name and parameter count whose arguments convert.</summary>
    public static MethodInfo? SelectMethod(Type type, string clrName, int argCount, bool staticOnly)
    {
        var flags = BindingFlags.Public | (staticOnly ? BindingFlags.Static : BindingFlags.Instance);
        return type.GetMethods(flags)
            .Where(m => m.Name == clrName && m.GetParameters().Length == argCount)
            .OrderBy(m => 0)
            .FirstOrDefault();
    }
}

public sealed partial class RubyContext
{
    private readonly Dictionary<Type, RubyClass> _clrTypeWrappers = new();

    /// <summary>The set of top-level CLR namespace roots reachable as constants
    /// (e.g. "System"). Resolved lazily by the interpreter on constant miss.</summary>
    public ClrNamespaceModule? GetClrNamespaceRoot(string name)
        => name == "System" ? (_systemRoot ??= new ClrNamespaceModule(this, "System")) : null;
    private ClrNamespaceModule? _systemRoot;

    /// <summary>Wraps a CLR type as a RubyClass with reflection-based dispatch,
    /// caching one wrapper per type and registering it so instances route here.</summary>
    public RubyClass WrapClrType(Type type)
    {
        if (_clrTypeWrappers.TryGetValue(type, out var existing)) return existing;

        var cls = new RubyClass(type.FullName?.Replace('.', ':').Replace(":", "::") ?? type.Name, ObjectClass)
        {
            Allocator = (_, _) => throw RaiseTypeError($"allocator undefined for {type.Name}"),
        };
        _clrTypeWrappers[type] = cls;
        RegisterClrExtension(type, cls);

        // Instance dispatch via reflection (method_missing fallback).
        cls.DefineMethod("method_missing", RubyMethodInfo.FromBuiltin("method_missing", cls,
            (c, self, args, _) => ClrInstanceCall(type, self!, args)));

        // Class-level: .new and static method dispatch.
        var singleton = SingletonClassOf(cls);
        singleton.DefineMethod("new", RubyMethodInfo.FromBuiltin("new", singleton,
            (c, _, args, _) => ClrConstruct(type, args)));
        singleton.DefineMethod("method_missing", RubyMethodInfo.FromBuiltin("method_missing", singleton,
            (c, _, args, _) => ClrStaticCall(type, args)));

        return cls;
    }

    private object? ClrConstruct(Type type, object?[] args)
    {
        var ctor = type.GetConstructors()
            .FirstOrDefault(ci => ci.GetParameters().Length == args.Length)
            ?? throw RaiseArgumentError($"no matching constructor for {type.Name} with {args.Length} args");
        var converted = ConvertArgs(ctor.GetParameters(), args);
        return ctor.Invoke(converted);
    }

    private object? ClrInstanceCall(Type type, object self, object?[] args)
    {
        // args[0] is the method-name symbol (method_missing convention).
        var name = (args[0] as RubySymbol)?.Name ?? "";
        var rest = args.Skip(1).ToArray();
        var clrName = ClrInterop.PascalCase(name);

        // property read
        var prop = type.GetProperty(clrName);
        if (prop is not null && rest.Length == 0)
            return ClrInterop.FromClr(prop.GetValue(self));

        var method = ClrInterop.SelectMethod(type, clrName, rest.Length, staticOnly: false)
            ?? throw RaiseNoMethodError(self, name);
        var converted = ConvertArgs(method.GetParameters(), rest);
        return ClrInterop.FromClr(method.Invoke(self, converted));
    }

    private object? ClrStaticCall(Type type, object?[] args)
    {
        var name = (args[0] as RubySymbol)?.Name ?? "";
        var rest = args.Skip(1).ToArray();
        var clrName = ClrInterop.PascalCase(name);

        var prop = type.GetProperty(clrName, BindingFlags.Public | BindingFlags.Static);
        if (prop is not null && rest.Length == 0)
            return ClrInterop.FromClr(prop.GetValue(null));

        var field = type.GetField(clrName, BindingFlags.Public | BindingFlags.Static);
        if (field is not null && rest.Length == 0)
            return ClrInterop.FromClr(field.GetValue(null));

        var method = ClrInterop.SelectMethod(type, clrName, rest.Length, staticOnly: true)
            ?? throw RaiseError(NoMethodErrorClass, $"undefined static method '{name}' for {type.Name}");
        var converted = ConvertArgs(method.GetParameters(), rest);
        return ClrInterop.FromClr(method.Invoke(null, converted));
    }

    private static object?[] ConvertArgs(ParameterInfo[] parameters, object?[] args)
    {
        var converted = new object?[args.Length];
        for (int i = 0; i < args.Length; i++)
            converted[i] = ClrInterop.ToClr(args[i], parameters[i].ParameterType);
        return converted;
    }
}
