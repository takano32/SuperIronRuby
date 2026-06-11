using SuperIronRuby.Runtime;

namespace SuperIronRuby.Interpreter;

/// <summary>What kind of lexical scope a <see cref="RubyScope"/> is.</summary>
public enum ScopeKind
{
    TopLevel,
    Method,
    Block,
    Class,
    Eval,
}

/// <summary>
/// A lexical scope: local variables plus the surrounding execution context
/// (self, the definition target for def/constants, the current block, etc.).
/// Block scopes chain to their parent so closures can read and write outer
/// locals; method scopes are boundaries (locals do not leak across them).
/// </summary>
public sealed class RubyScope
{
    private readonly Dictionary<string, object?> _locals = new();

    public ScopeKind Kind { get; }
    public RubyScope? Parent { get; }

    /// <summary>The current <c>self</c>.</summary>
    public object? Self { get; set; }

    /// <summary>The module/class that <c>def</c>/constants/methods are defined on.</summary>
    public RubyModule? DefinitionTarget { get; set; }

    /// <summary>The block passed to the current method (for <c>yield</c>).</summary>
    public RubyProc? Block { get; set; }

    /// <summary>The method currently executing (for <c>super</c>/<c>__method__</c>).</summary>
    public RubyMethodInfo? CurrentMethod { get; set; }

    /// <summary>A unique frame id (used to target <c>return</c> unwinds).</summary>
    public long FrameId { get; }

    /// <summary>Lexical module nesting (innermost first) for constant lookup.</summary>
    public List<RubyModule> LexicalModules { get; }

    /// <summary>The id that <c>break</c>/<c>next</c>/<c>redo</c> target: a loop's
    /// unique id, or a block's source id. 0 = no enclosing breakable construct.</summary>
    public long CurrentBreakId { get; set; }

    private static long _frameCounter;

    public RubyScope(ScopeKind kind, RubyScope? parent)
    {
        Kind = kind;
        Parent = parent;
        FrameId = ++_frameCounter;
        if (parent is not null)
        {
            Self = parent.Self;
            DefinitionTarget = parent.DefinitionTarget;
            Block = parent.Block;
            CurrentMethod = parent.CurrentMethod;
            LexicalModules = parent.LexicalModules;
            CurrentBreakId = parent.CurrentBreakId;
        }
        else
        {
            LexicalModules = new List<RubyModule>();
        }
    }

    /// <summary>Whether <paramref name="kind"/> is a local-variable boundary
    /// (method/top-level/class/eval do not see an enclosing block's locals).</summary>
    private static bool IsBoundary(ScopeKind kind) => kind != ScopeKind.Block;

    /// <summary>Reads a local, walking into parent block scopes; returns false if unset.</summary>
    public bool TryGetLocal(string name, out object? value)
    {
        var scope = this;
        while (scope is not null)
        {
            if (scope._locals.TryGetValue(name, out value)) return true;
            if (IsBoundary(scope.Kind)) break;   // do not cross a method/class boundary
            scope = scope.Parent;
        }
        value = null;
        return false;
    }

    /// <summary>Returns true if a local is visible (defined here or in an enclosing
    /// block scope).</summary>
    public bool HasLocal(string name) => TryGetLocal(name, out _);

    /// <summary>Assigns a local. If the name already exists in an enclosing block
    /// scope, that binding is updated; otherwise it is created in this scope.</summary>
    public void SetLocal(string name, object? value)
    {
        var scope = this;
        while (scope is not null)
        {
            if (scope._locals.ContainsKey(name))
            {
                scope._locals[name] = value;
                return;
            }
            if (IsBoundary(scope.Kind)) break;
            scope = scope.Parent;
        }
        _locals[name] = value;
    }

    /// <summary>Declares/sets a local in THIS scope unconditionally.</summary>
    public void DeclareLocal(string name, object? value) => _locals[name] = value;
}
