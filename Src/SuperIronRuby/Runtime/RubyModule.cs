namespace SuperIronRuby.Runtime;

/// <summary>
/// A Ruby module: a named bag of methods and constants that can be mixed into
/// classes via include/prepend. <see cref="RubyClass"/> extends this with a
/// superclass chain.
/// </summary>
public class RubyModule
{
    /// <summary>The module's name, or null for an anonymous module.</summary>
    public string? Name;

    private readonly Dictionary<string, RubyMethodInfo> _methods = new();
    private readonly Dictionary<string, object?> _constants = new();
    private readonly Dictionary<string, object?> _classVariables = new();

    // Declaration-order lists; ancestor linearization dedups and orders them.
    private readonly List<RubyModule> _includes = new();
    private readonly List<RubyModule> _prepends = new();

    // Cached ancestor linearization, invalidated via the global version counter.
    private IReadOnlyList<RubyModule>? _ancestorsCache;
    private long _ancestorsCacheVersion = -1;

    /// <summary>
    /// Bumped on any change that can affect method resolution or ancestry
    /// anywhere (DefineMethod, include, prepend, superclass change). Method and
    /// ancestor caches validate against this.
    /// </summary>
    public static long GlobalVersion { get; private set; }

    /// <summary>Increments <see cref="GlobalVersion"/> (call after a mutation).</summary>
    private protected static void BumpGlobalVersion() => GlobalVersion++;

    /// <summary>Directly included modules, in declaration order.</summary>
    public IReadOnlyList<RubyModule> IncludedModules => _includes;

    /// <summary>Directly prepended modules, in declaration order.</summary>
    public IReadOnlyList<RubyModule> PrependedModules => _prepends;

    // ---- methods -----------------------------------------------------------

    /// <summary>Defines (or replaces) a method in this module's own table.</summary>
    public void DefineMethod(string name, RubyMethodInfo method)
    {
        _methods[name] = method;
        BumpGlobalVersion();
    }

    /// <summary>Returns this module's own entry for <paramref name="name"/>, or null.</summary>
    public RubyMethodInfo? GetOwnMethod(string name)
        => _methods.TryGetValue(name, out var m) ? m : null;

    /// <summary>Removes a method from this module's own table (Ruby remove_method).</summary>
    public bool RemoveMethod(string name)
    {
        var removed = _methods.Remove(name);
        if (removed) BumpGlobalVersion();
        return removed;
    }

    /// <summary>Resolves a method by walking <see cref="Ancestors"/>.</summary>
    public RubyMethodInfo? LookupMethod(string name)
    {
        foreach (var mod in Ancestors)
        {
            var m = mod.GetOwnMethod(name);
            if (m is not null) return m;
        }
        return null;
    }

    /// <summary>Creates an alias: copies the resolved method under a new name.</summary>
    public bool AliasMethod(string newName, string oldName)
    {
        var resolved = LookupMethod(oldName);
        if (resolved is null) return false;
        DefineMethod(newName, resolved.CloneAs(newName, this));
        return true;
    }

    /// <summary>This module's own method names (no ancestors).</summary>
    public IEnumerable<string> OwnMethodNames => _methods.Keys;

    // ---- constants ---------------------------------------------------------

    /// <summary>Sets a constant in this module's own table.</summary>
    public void SetConstant(string name, object? value) => _constants[name] = value;

    /// <summary>Looks up a constant by walking ancestors (own tables only).</summary>
    public bool TryGetConstant(string name, out object? value)
    {
        foreach (var mod in Ancestors)
        {
            if (mod._constants.TryGetValue(name, out value)) return true;
        }
        value = null;
        return false;
    }

    /// <summary>Looks up a constant in this module's own table only.</summary>
    public bool TryGetOwnConstant(string name, out object? value)
        => _constants.TryGetValue(name, out value);

    /// <summary>This module's own constant names.</summary>
    public IEnumerable<string> OwnConstantNames => _constants.Keys;

    // ---- class variables ---------------------------------------------------

    /// <summary>Class variables stored on this module (lookup/target walks
    /// ancestors at a higher layer).</summary>
    public Dictionary<string, object?> ClassVariables => _classVariables;

    // ---- include / prepend -------------------------------------------------

    /// <summary>Mixes a module in (Ruby include). Newer includes resolve closer
    /// to this module. Re-including an already-present module is harmless (the
    /// linearization dedups it).</summary>
    public void Include(RubyModule mod)
    {
        _includes.Add(mod);
        BumpGlobalVersion();
    }

    /// <summary>Mixes a module in ahead of this module (Ruby prepend).</summary>
    public void Prepend(RubyModule mod)
    {
        _prepends.Add(mod);
        BumpGlobalVersion();
    }

    // ---- ancestors ---------------------------------------------------------

    /// <summary>
    /// The MRI-style ancestor linearization: prepended modules (newest first),
    /// self, included modules (newest first), then the superclass chain (for
    /// classes), with each module deduped to its first-established position.
    /// Cached and invalidated via <see cref="GlobalVersion"/>.
    /// </summary>
    public IReadOnlyList<RubyModule> Ancestors
    {
        get
        {
            if (_ancestorsCache is not null && _ancestorsCacheVersion == GlobalVersion)
                return _ancestorsCache;

            var result = BuildAncestors();
            _ancestorsCache = result;
            _ancestorsCacheVersion = GlobalVersion;
            return result;
        }
    }

    /// <summary>
    /// Layers onto the superclass base. Final order is
    /// [prepends ++ self ++ includes ++ super-base]; included/prepended modules
    /// contribute their own full linearization, and any module already present
    /// is skipped (keeping its first-established position).
    /// </summary>
    private List<RubyModule> BuildAncestors()
    {
        // Base: the inherited superclass chain (empty for a bare module).
        var result = new List<RubyModule>(SuperclassAncestors());

        // Included modules, oldest -> newest. Each contributes its ancestors,
        // skipping any already present; the block is prepended so newer includes
        // end up closer to self.
        var includeLayer = new List<RubyModule>();
        foreach (var mod in _includes)
        {
            var fresh = new List<RubyModule>();
            foreach (var anc in mod.Ancestors)
            {
                if (!result.Contains(anc) && !includeLayer.Contains(anc))
                    fresh.Add(anc);
            }
            includeLayer.InsertRange(0, fresh);
        }

        // self, ahead of the include layer.
        var middle = new List<RubyModule> { this };
        middle.AddRange(includeLayer);
        middle.AddRange(result);

        // Prepended modules, oldest -> newest, each block inserted at the front.
        var prependLayer = new List<RubyModule>();
        foreach (var mod in _prepends)
        {
            var fresh = new List<RubyModule>();
            foreach (var anc in mod.Ancestors)
            {
                if (!middle.Contains(anc) && !prependLayer.Contains(anc))
                    fresh.Add(anc);
            }
            prependLayer.InsertRange(0, fresh);
        }

        var final = new List<RubyModule>(prependLayer.Count + middle.Count);
        final.AddRange(prependLayer);
        final.AddRange(middle);
        return final;
    }

    /// <summary>The superclass ancestor chain that forms the base of this
    /// module's linearization. Empty for a bare module; <see cref="RubyClass"/>
    /// overrides it with the superclass's ancestors.</summary>
    private protected virtual IEnumerable<RubyModule> SuperclassAncestors()
        => Enumerable.Empty<RubyModule>();

    /// <summary>Invalidates ancestor caches globally (used by RubyClass on
    /// superclass change).</summary>
    private protected static void InvalidateAllAncestors() => BumpGlobalVersion();

    public override string ToString() => Name ?? "#<Module>";
}
