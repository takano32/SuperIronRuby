namespace SuperIronRuby.Runtime;

// The standard exception hierarchy and raise helpers (task R5).
// Parent relationships and message formats were verified against ruby 4.0.2.
public sealed partial class RubyContext
{
    // ---- common class handles ---------------------------------------------
    public RubyClass ExceptionClass { get; private set; } = null!;
    public RubyClass StandardErrorClass { get; private set; } = null!;
    public RubyClass ScriptErrorClass { get; private set; } = null!;
    public RubyClass RuntimeErrorClass { get; private set; } = null!;
    public RubyClass ArgumentErrorClass { get; private set; } = null!;
    public RubyClass TypeErrorClass { get; private set; } = null!;
    public RubyClass NameErrorClass { get; private set; } = null!;
    public RubyClass NoMethodErrorClass { get; private set; } = null!;
    public RubyClass ZeroDivisionErrorClass { get; private set; } = null!;
    public RubyClass IndexErrorClass { get; private set; } = null!;
    public RubyClass KeyErrorClass { get; private set; } = null!;
    public RubyClass RangeErrorClass { get; private set; } = null!;
    public RubyClass StopIterationClass { get; private set; } = null!;
    public RubyClass FrozenErrorClass { get; private set; } = null!;
    public RubyClass LocalJumpErrorClass { get; private set; } = null!;
    public RubyClass NotImplementedErrorClass { get; private set; } = null!;
    public RubyClass IOErrorClass { get; private set; } = null!;
    public RubyClass SystemExitClass { get; private set; } = null!;
    public RubyClass NoMatchingPatternErrorClass { get; private set; } = null!;
    public RubyClass NoMatchingPatternKeyErrorClass { get; private set; } = null!;
    public RubyClass RuntimeErrorOrStandard => RuntimeErrorClass;

    /// <summary>The current exception ($!), set while a rescue body runs.</summary>
    public RubyExceptionObject? CurrentException { get; set; }

    // R3's Bootstrap calls this partial method.
    partial void BootstrapExceptions()
    {
        ExceptionClass = DefineClass("Exception", ObjectClass);
        ExceptionClass.Allocator = (_, cls) => new RubyExceptionObject(cls);

        // Direct children of Exception.
        ScriptErrorClass = Sub("ScriptError", ExceptionClass);
        Sub("NoMemoryError", ExceptionClass);
        var signalException = Sub("SignalException", ExceptionClass);
        Sub("Interrupt", signalException);
        Sub("SecurityError", ExceptionClass);
        Sub("SystemStackError", ExceptionClass);
        SystemExitClass = Sub("SystemExit", ExceptionClass);
        StandardErrorClass = Sub("StandardError", ExceptionClass);

        // ScriptError children.
        Sub("LoadError", ScriptErrorClass);
        NotImplementedErrorClass = Sub("NotImplementedError", ScriptErrorClass);
        Sub("SyntaxError", ScriptErrorClass);

        // StandardError children.
        ArgumentErrorClass = Sub("ArgumentError", StandardErrorClass);
        Sub("EncodingError", StandardErrorClass);
        Sub("FiberError", StandardErrorClass);
        IOErrorClass = Sub("IOError", StandardErrorClass);
        Sub("EOFError", IOErrorClass);
        IndexErrorClass = Sub("IndexError", StandardErrorClass);
        KeyErrorClass = Sub("KeyError", IndexErrorClass);
        StopIterationClass = Sub("StopIteration", IndexErrorClass);
        LocalJumpErrorClass = Sub("LocalJumpError", StandardErrorClass);
        NameErrorClass = Sub("NameError", StandardErrorClass);
        NoMethodErrorClass = Sub("NoMethodError", NameErrorClass);
        RangeErrorClass = Sub("RangeError", StandardErrorClass);
        Sub("FloatDomainError", RangeErrorClass);
        Sub("RegexpError", StandardErrorClass);
        RuntimeErrorClass = Sub("RuntimeError", StandardErrorClass);
        FrozenErrorClass = Sub("FrozenError", RuntimeErrorClass);
        Sub("ThreadError", StandardErrorClass);
        TypeErrorClass = Sub("TypeError", StandardErrorClass);
        ZeroDivisionErrorClass = Sub("ZeroDivisionError", StandardErrorClass);
        NoMatchingPatternErrorClass = Sub("NoMatchingPatternError", StandardErrorClass);
        NoMatchingPatternKeyErrorClass = Sub("NoMatchingPatternKeyError", NoMatchingPatternErrorClass);
    }

    // Defines an exception subclass that allocates RubyExceptionObject.
    private RubyClass Sub(string name, RubyClass super)
    {
        var cls = DefineClass(name, super);
        cls.Allocator = (_, c) => new RubyExceptionObject(c);
        return cls;
    }

    // ---- creation & raising -----------------------------------------------

    /// <summary>Creates an exception instance with a message (no Ruby dispatch).</summary>
    public RubyExceptionObject CreateException(RubyClass cls, string message)
        => new(cls) { Message = message };

    /// <summary>Builds a <see cref="RubyRaiseException"/> for <c>throw</c>-style use:
    /// <c>throw ctx.RaiseError(cls, "msg");</c></summary>
    public RubyRaiseException RaiseError(RubyClass cls, string message)
        => new(CreateException(cls, message));

    public RubyRaiseException RaiseTypeError(string message)
        => RaiseError(TypeErrorClass, message);

    public RubyRaiseException RaiseArgumentError(string message)
        => RaiseError(ArgumentErrorClass, message);

    public RubyRaiseException RaiseRuntimeError(string message)
        => RaiseError(RuntimeErrorClass, message);

    public RubyRaiseException RaiseNameError(string message)
        => RaiseError(NameErrorClass, message);

    public RubyRaiseException RaiseZeroDivisionError(string message = "divided by 0")
        => RaiseError(ZeroDivisionErrorClass, message);

    public RubyRaiseException RaiseNotImplementedError(string message)
        => RaiseError(NotImplementedErrorClass, message);

    /// <summary>FrozenError, e.g. "can't modify frozen String: \"x\"".</summary>
    public RubyRaiseException RaiseFrozenError(string typeName, string inspectedReceiver)
        => RaiseError(FrozenErrorClass, $"can't modify frozen {typeName}: {inspectedReceiver}");

    /// <summary>NoMethodError: "undefined method 'm' for an instance of T"
    /// (or "... for nil/true/false/main").</summary>
    public RubyRaiseException RaiseNoMethodError(object? receiver, string methodName)
        => RaiseError(NoMethodErrorClass,
            $"undefined method '{methodName}' for {DescribeReceiver(receiver)}");

    /// <summary>NoMethodError for a private call:
    /// "private method 'm' called for an instance of T".</summary>
    public RubyRaiseException RaisePrivateMethodError(object? receiver, string methodName)
        => RaiseError(NoMethodErrorClass,
            $"private method '{methodName}' called for {DescribeReceiver(receiver)}");

    public RubyRaiseException RaiseArityError(int given, int expectedMin, int expectedMax)
    {
        string expected = expectedMax < 0
            ? $"{expectedMin}+"
            : expectedMin == expectedMax
                ? expectedMin.ToString()
                : $"{expectedMin}..{expectedMax}";
        return RaiseArgumentError($"wrong number of arguments (given {given}, expected {expected})");
    }

    // MRI describes the receiver as "an instance of C", or specially for the
    // singletons nil/true/false and the main object.
    private string DescribeReceiver(object? receiver)
    {
        switch (receiver)
        {
            case null: return "nil";
            case true: return "true";
            case false: return "false";
            default:
                if (ReferenceEquals(receiver, MainObject)) return "main:Object";
                var cls = GetClassOf(receiver);
                // Modules/classes describe themselves by name.
                if (receiver is RubyModule m) return $"{m.Name ?? cls.Name}:{cls.Name}";
                return $"an instance of {cls.Name}";
        }
    }
}
