namespace SuperIronRuby.Runtime;

/// <summary>Flags describing how a method is being called.</summary>
[System.Flags]
public enum RubyCallFlags
{
    None = 0,
    /// <summary>An implicit-self call (FCall, e.g. <c>foo</c> with no receiver):
    /// private methods are callable.</summary>
    ImplicitSelf = 1,
    /// <summary>A variable-or-call (VCall, e.g. a bare identifier).</summary>
    VCall = 2,
}

// Method dispatch: Send, the method cache, method_missing, visibility (task R4).
public sealed partial class RubyContext
{
    // Method cache keyed by (lookup class, method name). Each entry records the
    // GlobalVersion at which it was computed; a stale entry is recomputed. This
    // is a naive global-invalidation cache (any module mutation bumps the
    // version). Not thread-safe (single-engine assumption).
    private readonly Dictionary<(RubyModule, string), (RubyMethodInfo? method, long version)> _methodCache = new();

    /// <summary>Resolves a method for <paramref name="receiver"/> by name,
    /// honoring singleton classes, or null if undefined.</summary>
    public RubyMethodInfo? ResolveMethod(object? receiver, string name)
    {
        var klass = GetImmediateClassOf(receiver);
        return ResolveInClass(klass, name);
    }

    private RubyMethodInfo? ResolveInClass(RubyModule klass, string name)
    {
        var key = (klass, name);
        if (_methodCache.TryGetValue(key, out var entry) && entry.version == RubyModule.GlobalVersion)
            return entry.method;

        var resolved = klass.LookupMethod(name);
        _methodCache[key] = (resolved, RubyModule.GlobalVersion);
        return resolved;
    }

    /// <summary>True if the method chain of <paramref name="value"/> includes
    /// <paramref name="module"/> (kind_of?).</summary>
    // (IsKindOf lives in RubyContext.cs.)

    /// <summary>
    /// The central dispatch entry point. Resolves and invokes
    /// <paramref name="name"/> on <paramref name="receiver"/>, applying
    /// visibility rules and falling back to method_missing / NoMethodError.
    /// </summary>
    public object? Send(object? receiver, string name, object?[] args, RubyProc? block = null,
        RubyCallFlags flags = RubyCallFlags.None, object? callerSelf = null)
    {
        var method = ResolveMethod(receiver, name);
        if (method is null)
            return InvokeMethodMissing(receiver, name, args, block);

        CheckVisibility(method, receiver, name, flags, callerSelf);
        return Invoke(method, receiver, args, block);
    }

    /// <summary>A public-only send (the <c>.</c> form). Private methods are not
    /// callable; visibility violations fall through to method_missing.</summary>
    public object? SendPublic(object? receiver, string name, object?[] args, RubyProc? block = null,
        object? callerSelf = null)
        => Send(receiver, name, args, block, RubyCallFlags.None, callerSelf);

    private void CheckVisibility(RubyMethodInfo method, object? receiver, string name,
        RubyCallFlags flags, object? callerSelf)
    {
        switch (method.Visibility)
        {
            case RubyMethodVisibility.Public:
                return;

            case RubyMethodVisibility.Private:
                // Private methods are callable only via an implicit-self call
                // (FCall) or as an attribute-write on self (name ends with '=').
                if ((flags & RubyCallFlags.ImplicitSelf) != 0) return;
                if (name.EndsWith('=')) return;
                throw RaisePrivateMethodError(receiver, name);

            case RubyMethodVisibility.Protected:
                // Callable only when callerSelf is kind_of the method's owner.
                if (callerSelf is not null && IsKindOf(callerSelf, method.Owner)) return;
                throw RaiseError(NoMethodErrorClass,
                    $"protected method '{name}' called for {DescribeReceiverPublic(receiver)}");
        }
    }

    // method_missing fallback: dispatch :method_missing with the symbol name
    // prepended, or raise NoMethodError if method_missing itself is undefined
    // (i.e. only BasicObject's default would apply, which we model as the raise).
    private object? InvokeMethodMissing(object? receiver, string name, object?[] args, RubyProc? block)
    {
        var mm = ResolveMethod(receiver, "method_missing");
        if (mm is not null && !ReferenceEquals(mm.Builtin, DefaultMethodMissingMarker))
        {
            var mmArgs = new object?[args.Length + 1];
            mmArgs[0] = Intern(name);
            System.Array.Copy(args, 0, mmArgs, 1, args.Length);
            return Invoke(mm, receiver, mmArgs, block);
        }
        throw RaiseNoMethodError(receiver, name);
    }

    /// <summary>Marker used to recognize the built-in default method_missing so
    /// the dispatcher raises NoMethodError instead of recursing.</summary>
    public static readonly BuiltinMethodBody DefaultMethodMissingMarker = (_, _, _, _) => null;

    /// <summary>Invokes a resolved method (builtin or interpreted), enforcing
    /// arity for builtins.</summary>
    public object? Invoke(RubyMethodInfo method, object? self, object?[] args, RubyProc? block)
    {
        if (method.Builtin is not null)
        {
            CheckArity(method, args.Length);
            return method.Builtin(this, self, args, block);
        }

        if (InterpretedInvoker is null)
            throw new InvalidOperationException(
                "interpreter not installed: cannot invoke a Ruby-defined method");
        return InterpretedInvoker(method, self, args, block);
    }

    private void CheckArity(RubyMethodInfo method, int given)
    {
        if (given < method.ArityMin || (method.ArityMax >= 0 && given > method.ArityMax))
            throw RaiseArityError(given, method.ArityMin, method.ArityMax);
    }

    /// <summary>respond_to? — true if a public (or, when
    /// <paramref name="includeAll"/>, any) method is defined.</summary>
    public bool RespondTo(object? receiver, string name, bool includeAll = false)
    {
        var method = ResolveMethod(receiver, name);
        if (method is null) return false;
        if (includeAll) return true;
        return method.Visibility == RubyMethodVisibility.Public;
    }

    // Like DescribeReceiver but used for protected errors (same wording).
    private string DescribeReceiverPublic(object? receiver) => DescribeReceiverForError(receiver);

    private string DescribeReceiverForError(object? receiver)
        => receiver switch
        {
            null => "nil",
            true => "true",
            false => "false",
            RubyModule m => $"{m.Name}",
            _ => $"an instance of {GetClassOf(receiver).Name}",
        };
}
