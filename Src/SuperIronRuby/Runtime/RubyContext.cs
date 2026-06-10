using System.Numerics;

namespace SuperIronRuby.Runtime;

/// <summary>
/// The per-engine Ruby world: the core class hierarchy, the symbol table,
/// globals, and (in later partials) method dispatch and the exception
/// hierarchy. Constructing a <see cref="RubyContext"/> bootstraps the core
/// classes exactly as MRI does (BasicObject ← Object ← Module ← Class, with
/// Kernel mixed into Object).
/// </summary>
public sealed partial class RubyContext
{
    // ---- core hierarchy ----------------------------------------------------
    public RubyClass BasicObjectClass { get; private set; } = null!;
    public RubyClass ObjectClass { get; private set; } = null!;
    public RubyClass ModuleClass { get; private set; } = null!;
    public RubyClass ClassClass { get; private set; } = null!;
    public RubyModule KernelModule { get; private set; } = null!;
    public RubyModule ComparableModule { get; private set; } = null!;
    public RubyModule EnumerableModule { get; private set; } = null!;

    public RubyClass NilClass { get; private set; } = null!;
    public RubyClass TrueClass { get; private set; } = null!;
    public RubyClass FalseClass { get; private set; } = null!;
    public RubyClass NumericClass { get; private set; } = null!;
    public RubyClass IntegerClass { get; private set; } = null!;
    public RubyClass FloatClass { get; private set; } = null!;
    public RubyClass StringClass { get; private set; } = null!;
    public RubyClass SymbolClass { get; private set; } = null!;
    public RubyClass ArrayClass { get; private set; } = null!;
    public RubyClass HashClass { get; private set; } = null!;
    public RubyClass RangeClass { get; private set; } = null!;
    public RubyClass RegexpClass { get; private set; } = null!;
    public RubyClass MatchDataClass { get; private set; } = null!;
    public RubyClass ProcClass { get; private set; } = null!;
    public RubyClass MethodClass { get; private set; } = null!;

    // ---- main object, globals, I/O ----------------------------------------
    public RubyObject MainObject { get; private set; } = null!;
    public System.IO.TextWriter Stdout { get; set; } = Console.Out;
    public System.IO.TextWriter Stderr { get; set; } = Console.Error;
    public System.IO.TextReader Stdin { get; set; } = Console.In;

    private readonly Dictionary<string, RubySymbol> _symbols = new();
    private readonly Dictionary<string, object?> _globals = new();
    private readonly Dictionary<Type, RubyClass> _clrExtensions = new();

    /// <summary>Routes instances of a CLR type to a Ruby class (used by the
    /// builtin loader's <c>[RubyClass(Extends = ...)]</c>).</summary>
    public void RegisterClrExtension(Type clrType, RubyClass rubyClass)
        => _clrExtensions[clrType] = rubyClass;

    /// <summary>
    /// Hook into the interpreter for invoking Ruby-defined methods. Installed at
    /// engine startup so the runtime has no compile-time dependency on the
    /// interpreter. Args: (method, self, args, block) -> result.
    /// </summary>
    public Func<RubyMethodInfo, object?, object?[], RubyProc?, object?>? InterpretedInvoker;

    public RubyContext()
    {
        Bootstrap();
    }

    /// <summary>Builds the standard exception hierarchy. Implemented by the R5
    /// partial (Exceptions.cs); a no-op until then.</summary>
    partial void BootstrapExceptions();

    private void Bootstrap()
    {
        // The metaclass triad. BasicObject has no superclass.
        BasicObjectClass = new RubyClass("BasicObject", null);
        ObjectClass = new RubyClass("Object", BasicObjectClass);
        ModuleClass = new RubyClass("Module", ObjectClass);
        ClassClass = new RubyClass("Class", ModuleClass);

        KernelModule = DefineModule("Kernel");
        ObjectClass.Include(KernelModule);

        ComparableModule = DefineModule("Comparable");
        EnumerableModule = DefineModule("Enumerable");

        // Register the foundational classes as constants on Object.
        Register(BasicObjectClass);
        Register(ObjectClass);
        Register(ModuleClass);
        Register(ClassClass);

        NilClass = DefineClass("NilClass", ObjectClass);
        TrueClass = DefineClass("TrueClass", ObjectClass);
        FalseClass = DefineClass("FalseClass", ObjectClass);

        NumericClass = DefineClass("Numeric", ObjectClass);
        NumericClass.Include(ComparableModule);
        IntegerClass = DefineClass("Integer", NumericClass);
        FloatClass = DefineClass("Float", NumericClass);

        StringClass = DefineClass("String", ObjectClass);
        StringClass.Include(ComparableModule);
        SymbolClass = DefineClass("Symbol", ObjectClass);
        SymbolClass.Include(ComparableModule);

        ArrayClass = DefineClass("Array", ObjectClass);
        ArrayClass.Include(EnumerableModule);
        HashClass = DefineClass("Hash", ObjectClass);
        HashClass.Include(EnumerableModule);
        RangeClass = DefineClass("Range", ObjectClass);
        RangeClass.Include(EnumerableModule);

        RegexpClass = DefineClass("Regexp", ObjectClass);
        MatchDataClass = DefineClass("MatchData", ObjectClass);
        ProcClass = DefineClass("Proc", ObjectClass);
        MethodClass = DefineClass("Method", ObjectClass);

        // The exception hierarchy is built by the R5 partial.
        BootstrapExceptions();

        MainObject = new RubyObject(ObjectClass);

        _globals["$stdout"] = null; // I/O objects are wired by the builtins layer
        _globals["$stderr"] = null;
        _globals["$stdin"] = null;
    }

    // ---- module/class definition ------------------------------------------

    /// <summary>Defines (or returns an existing) module as a constant on Object.</summary>
    public RubyModule DefineModule(string name)
    {
        if (ObjectClass is not null && ObjectClass.TryGetOwnConstant(name, out var existing))
        {
            if (existing is RubyModule m && existing is not RubyClass) return m;
            throw new InvalidOperationException($"{name} is not a module");
        }
        var mod = new RubyModule { Name = name };
        Register(mod);
        return mod;
    }

    /// <summary>Defines (or reopens) a class as a constant on Object.</summary>
    public RubyClass DefineClass(string name, RubyClass superclass)
    {
        if (ObjectClass.TryGetOwnConstant(name, out var existing))
        {
            if (existing is RubyClass c) return c;
            throw new InvalidOperationException($"{name} is not a class");
        }
        var cls = new RubyClass(name, superclass);
        Register(cls);
        return cls;
    }

    private void Register(RubyModule mod)
    {
        if (mod.Name is not null)
            ObjectClass.SetConstant(mod.Name, mod);
    }

    // ---- class-of ----------------------------------------------------------

    /// <summary>The logical (non-singleton) Ruby class of a value.</summary>
    public RubyClass GetClassOf(object? value)
    {
        switch (value)
        {
            case null: return NilClass;
            case bool b: return b ? TrueClass : FalseClass;
            case long: return IntegerClass;
            case BigInteger: return IntegerClass;
            case double: return FloatClass;
            case MutableString: return StringClass;
            case RubySymbol: return SymbolClass;
            case RubyArray: return ArrayClass;
            case RubyHash: return HashClass;
            case RubyRange: return RangeClass;
            case RubyRegexp: return RegexpClass;
            case RubyProc: return ProcClass;
            case RubyClass: return ClassClass;
            case RubyModule: return ModuleClass;
            case RubyExceptionObject ex: return ex.RubyClass;
            case RubyObject obj: return obj.RubyClass;
            default:
                // Custom CLR types registered via [RubyClass(Extends = ...)].
                if (_clrExtensions.TryGetValue(value.GetType(), out var ext)) return ext;
                return ObjectClass;
        }
    }

    /// <summary>The class used for method lookup: the value's singleton class if
    /// it has one, else its logical class.</summary>
    public RubyClass GetImmediateClassOf(object? value)
    {
        switch (value)
        {
            case RubyObject obj when obj.SingletonClass is not null:
                return obj.SingletonClass;
            case RubyModule mod when mod.SingletonClass is not null:
                return mod.SingletonClass;
            default:
                return GetClassOf(value);
        }
    }

    /// <summary>
    /// Returns (creating if needed) the singleton class of an object. For modules
    /// and classes the metaclass is stored on the module; for ordinary objects on
    /// the object. Immediate values (numbers, symbols, nil/true/false) cannot
    /// carry a per-object singleton — their logical class is returned (defining a
    /// singleton method on nil/true/false therefore targets NilClass etc., as in
    /// MRI).
    /// </summary>
    public RubyClass SingletonClassOf(object? value)
    {
        switch (value)
        {
            case RubyModule mod:
                if (mod.SingletonClass is null)
                {
                    var super = mod is RubyClass { Superclass: { } sc }
                        ? (sc.SingletonClass ?? ClassClass)
                        : ClassClass;
                    mod.SingletonClass = new RubyClass($"#<Class:{mod.Name}>", super)
                    {
                        IsSingletonClass = true,
                        AttachedObject = mod,
                    };
                }
                return mod.SingletonClass;

            case RubyObject obj:
                if (obj.SingletonClass is null)
                {
                    obj.SingletonClass = new RubyClass($"#<Class:#<{obj.RubyClass.Name}>>", obj.RubyClass)
                    {
                        IsSingletonClass = true,
                        AttachedObject = obj,
                    };
                }
                return obj.SingletonClass;

            default:
                // Immediates: no per-object singleton; the logical class stands in.
                return GetClassOf(value);
        }
    }

    // ---- symbols & globals -------------------------------------------------

    /// <summary>Interns a name to a unique, reference-equal <see cref="RubySymbol"/>.</summary>
    public RubySymbol Intern(string name)
    {
        if (_symbols.TryGetValue(name, out var sym)) return sym;
        sym = new RubySymbol(name);
        _symbols[name] = sym;
        return sym;
    }

    /// <summary>Reads a global variable, or null (nil) if unset.</summary>
    public object? GetGlobal(string name)
        => _globals.TryGetValue(name, out var v) ? v : null;

    /// <summary>Sets a global variable.</summary>
    public void SetGlobal(string name, object? value) => _globals[name] = value;

    // ---- value helpers -----------------------------------------------------

    /// <summary>True if <paramref name="value"/>'s class chain includes <paramref name="module"/>.</summary>
    public bool IsKindOf(object? value, RubyModule module)
        => GetClassOf(value).Ancestors.Contains(module);
}
