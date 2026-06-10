using System.Reflection;

namespace SuperIronRuby.Runtime;

/// <summary>
/// Loads builtin classes/modules declared with <see cref="RubyClassAttribute"/>
/// / <see cref="RubyModuleAttribute"/> and their <see cref="RubyMethodAttribute"/>
/// methods into a <see cref="RubyContext"/> (IronRuby-style). Deterministic:
/// types are processed ordered by name so reopen semantics are stable.
/// </summary>
public static class BuiltinLoader
{
    public static void LoadAssembly(RubyContext ctx, Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<RubyClassAttribute>() is not null
                     || t.GetCustomAttribute<RubyModuleAttribute>() is not null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

        // Pass 1: ensure every class/module exists, record CLR extension mappings.
        var targets = new Dictionary<Type, RubyModule>();
        foreach (var type in types)
        {
            var classAttr = type.GetCustomAttribute<RubyClassAttribute>();
            if (classAttr is not null)
            {
                var cls = ResolveOrCreateClass(ctx, classAttr);
                targets[type] = cls;
                if (classAttr.Extends is not null)
                    ctx.RegisterClrExtension(classAttr.Extends, cls);
                continue;
            }

            var moduleAttr = type.GetCustomAttribute<RubyModuleAttribute>()!;
            targets[type] = ResolveOrCreateModule(ctx, moduleAttr);
        }

        // Pass 2: bind methods.
        foreach (var type in types)
        {
            var target = targets[type];
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                foreach (var attr in method.GetCustomAttributes<RubyMethodAttribute>())
                    BindMethod(ctx, target, method, attr);
            }
        }
    }

    private static RubyClass ResolveOrCreateClass(RubyContext ctx, RubyClassAttribute attr)
    {
        if (ctx.ObjectClass.TryGetOwnConstant(attr.Name, out var existing) && existing is RubyClass c)
            return c;

        if (!ctx.ObjectClass.TryGetConstant(attr.Inherits, out var superObj) || superObj is not RubyClass super)
            throw new InvalidOperationException(
                $"[RubyClass(\"{attr.Name}\")] Inherits=\"{attr.Inherits}\" is not a known class");

        return ctx.DefineClass(attr.Name, super);
    }

    private static RubyModule ResolveOrCreateModule(RubyContext ctx, RubyModuleAttribute attr)
    {
        if (ctx.ObjectClass.TryGetOwnConstant(attr.Name, out var existing)
            && existing is RubyModule m && existing is not RubyClass)
            return m;
        return ctx.DefineModule(attr.Name);
    }

    private static void BindMethod(RubyContext ctx, RubyModule target, MethodInfo method, RubyMethodAttribute attr)
    {
        BuiltinMethodBody body;
        try
        {
            body = (BuiltinMethodBody)Delegate.CreateDelegate(typeof(BuiltinMethodBody), method);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException(
                $"[RubyMethod(\"{attr.Name}\")] {method.DeclaringType?.Name}.{method.Name} must have signature " +
                "(RubyContext, object?, object?[], RubyProc?) -> object?");
        }

        switch (attr.Kind)
        {
            case RubyMethodKind.Instance:
                Define(target, attr.Name, body, attr.Visibility, attr.ArityMin, attr.ArityMax);
                break;

            case RubyMethodKind.Static:
                Define(ctx.SingletonClassOf(target), attr.Name, body,
                    RubyMethodVisibility.Public, attr.ArityMin, attr.ArityMax);
                break;

            case RubyMethodKind.ModuleFunction:
                // Private instance method + public singleton method.
                Define(target, attr.Name, body, RubyMethodVisibility.Private, attr.ArityMin, attr.ArityMax);
                Define(ctx.SingletonClassOf(target), attr.Name, body,
                    RubyMethodVisibility.Public, attr.ArityMin, attr.ArityMax);
                break;
        }
    }

    private static void Define(RubyModule target, string name, BuiltinMethodBody body,
        RubyMethodVisibility vis, int min, int max)
        => target.DefineMethod(name, RubyMethodInfo.FromBuiltin(name, target, body, vis, min, max));
}
