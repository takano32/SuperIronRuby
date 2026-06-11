using SuperIronRuby.Parser;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Interpreter;

/// <summary>How arguments are bound to parameters.</summary>
public enum BindMode
{
    /// <summary>Method call: strict arity, no auto-splat.</summary>
    Method,
    /// <summary>Lambda: strict arity like a method.</summary>
    Lambda,
    /// <summary>Block/proc: lenient — extra args dropped, missing become nil,
    /// a single array auto-splats across multiple parameters.</summary>
    ProcBlock,
}

public sealed partial class Interpreter
{
    /// <summary>
    /// Binds <paramref name="args"/> (and a trailing <see cref="KwArgsHash"/>, if
    /// present) to the parameters declared by <paramref name="parameters"/>,
    /// declaring locals in <paramref name="scope"/>. Follows Ruby's required /
    /// optional / rest / post / keyword / keyword-rest / block ordering.
    /// </summary>
    private void BindParameters(ParametersNode? parameters, object?[] args, RubyProc? block,
        RubyScope scope, BindMode mode)
    {
        if (parameters is null)
        {
            // No declared params. A method with no params and given args is an
            // arity error; blocks/lambdas tolerate it.
            return;
        }

        // Separate a trailing keyword-args hash from the positional arguments.
        RubyHash? kwargs = null;
        var positional = args;
        bool wantsKeywords = parameters.Keywords.Length > 0 || parameters.KeywordRest is not null;
        if (wantsKeywords && args.Length > 0 && args[^1] is KwArgsHash kw)
        {
            kwargs = kw;
            positional = args[..^1];
        }

        // Block auto-splat: a proc with multiple params called with a single
        // array spreads it.
        if (mode == BindMode.ProcBlock && positional.Length == 1 && positional[0] is RubyArray arr
            && CountPositionalParams(parameters) > 1)
        {
            positional = arr.ToArray();
        }

        int required = parameters.Requireds.Length;
        int optional = parameters.Optionals.Length;
        int post = parameters.Posts.Length;
        bool hasRest = parameters.Rest is not null;

        if (mode is BindMode.Method or BindMode.Lambda)
        {
            int min = required + post;
            int max = hasRest ? -1 : required + optional + post;
            if (positional.Length < min || (max >= 0 && positional.Length > max))
                throw _context.RaiseArityError(positional.Length, min, max < 0 ? -1 : max);
        }

        int index = 0;

        // Required (leading).
        foreach (var p in parameters.Requireds)
            BindOne(p, At(positional, index++, mode), scope);

        // Optionals — consume args while available (leaving enough for posts).
        int available = positional.Length - post - required;
        foreach (var opt in parameters.Optionals)
        {
            if (opt is OptionalParameterNode o)
            {
                if (available > 0)
                {
                    scope.DeclareLocal(o.Name, At(positional, index++, mode));
                    available--;
                }
                else
                {
                    scope.DeclareLocal(o.Name, Eval(o.Value, scope));   // default
                }
            }
        }

        // Rest — gathers the middle slice.
        if (parameters.Rest is RestParameterNode rest)
        {
            int restCount = Math.Max(0, positional.Length - index - post);
            var restArr = new RubyArray();
            for (int i = 0; i < restCount; i++) restArr.Add(positional[index++]);
            if (rest.Name is not null) scope.DeclareLocal(rest.Name, restArr);
        }

        // Post (trailing required).
        foreach (var p in parameters.Posts)
            BindOne(p, At(positional, index++, mode), scope);

        // Keywords.
        foreach (var kp in parameters.Keywords)
            BindKeyword(kp, kwargs, scope);

        // Keyword rest (**rest).
        if (parameters.KeywordRest is KeywordRestParameterNode krest && krest.Name is not null)
        {
            var remaining = new RubyHash();
            if (kwargs is not null)
            {
                var declared = DeclaredKeywordNames(parameters);
                foreach (var e in kwargs.Entries())
                    if (e.Key is RubySymbol s && !declared.Contains(s.Name))
                        remaining.Store(e.Key, e.Value);
            }
            scope.DeclareLocal(krest.Name, remaining);
        }

        // Block parameter (&blk).
        if (parameters.Block is BlockParameterNode bp && bp.Name is not null)
            scope.DeclareLocal(bp.Name, block);
    }

    private static object? At(object?[] args, int i, BindMode mode)
        => i < args.Length ? args[i] : null;   // ProcBlock/Lambda tolerate missing -> nil

    private void BindOne(Node param, object? value, RubyScope scope)
    {
        switch (param)
        {
            case RequiredParameterNode req:
                scope.DeclareLocal(req.Name, value);
                break;
            // Destructuring parameters `(a, b)` are handled once multiple
            // assignment lands (task I6); rare in practice.
        }
    }

    private void BindKeyword(Node kp, RubyHash? kwargs, RubyScope scope)
    {
        switch (kp)
        {
            case RequiredKeywordParameterNode req:
            {
                var key = _context.Intern(req.Name);
                if (kwargs is not null && kwargs.TryGetValue(key, out var v))
                    scope.DeclareLocal(req.Name, v);
                else
                    throw _context.RaiseArgumentError($"missing keyword: :{req.Name}");
                break;
            }
            case OptionalKeywordParameterNode opt:
            {
                var key = _context.Intern(opt.Name);
                if (kwargs is not null && kwargs.TryGetValue(key, out var v))
                    scope.DeclareLocal(opt.Name, v);
                else
                    scope.DeclareLocal(opt.Name, Eval(opt.Value, scope));
                break;
            }
        }
    }

    private static int CountPositionalParams(ParametersNode p)
        => p.Requireds.Length + p.Optionals.Length + p.Posts.Length + (p.Rest is not null ? 1 : 0);

    private static HashSet<string> DeclaredKeywordNames(ParametersNode p)
    {
        var set = new HashSet<string>();
        foreach (var k in p.Keywords)
        {
            if (k is RequiredKeywordParameterNode r) set.Add(r.Name);
            else if (k is OptionalKeywordParameterNode o) set.Add(o.Name);
        }
        return set;
    }
}
