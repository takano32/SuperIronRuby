using SuperIronRuby.Parser;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Interpreter;

// Classes, modules, constants, instance/class/global variables, defined? (I4).
public sealed partial class Interpreter
{
    // ---- class / module / singleton-class --------------------------------

    private object? EvalClass(ClassNode node, RubyScope scope)
    {
        var (container, name) = ResolveConstantTarget(node.ConstantPath, scope);

        RubyClass superclass = _context.ObjectClass;
        if (node.Superclass is not null)
        {
            if (Eval(node.Superclass, scope) is not RubyClass sc)
                throw _context.RaiseTypeError("superclass must be a Class");
            superclass = sc;
        }

        RubyClass cls;
        if (container.TryGetOwnConstant(name, out var existing))
        {
            if (existing is not RubyClass ec)
                throw _context.RaiseTypeError($"{name} is not a class");
            cls = ec;
        }
        else
        {
            cls = new RubyClass(QualifiedName(container, name), superclass);
            container.SetConstant(name, cls);
        }

        return EvalBodyInModule(cls, node.Body);
    }

    private object? EvalModule(ModuleNode node, RubyScope scope)
    {
        var (container, name) = ResolveConstantTarget(node.ConstantPath, scope);

        RubyModule mod;
        if (container.TryGetOwnConstant(name, out var existing))
        {
            if (existing is not RubyModule em || existing is RubyClass)
                throw _context.RaiseTypeError($"{name} is not a module");
            mod = em;
        }
        else
        {
            mod = new RubyModule { Name = QualifiedName(container, name) };
            container.SetConstant(name, mod);
        }

        return EvalBodyInModule(mod, node.Body);
    }

    private object? EvalSingletonClass(SingletonClassNode node, RubyScope scope)
    {
        var obj = Eval(node.Expression, scope);
        var singleton = _context.SingletonClassOf(obj);
        return EvalBodyInModule(singleton, node.Body);
    }

    private object? EvalBodyInModule(RubyModule mod, Node? body)
    {
        var classScope = new RubyScope(ScopeKind.Class, null)
        {
            Self = mod,
            DefinitionTarget = mod,
        };
        classScope.LexicalModules.Add(mod);
        return Eval(body, classScope);    // value of a class/module body is its last expr
    }

    private static string QualifiedName(RubyModule container, string name)
        => container.Name is null or "Object" ? name : $"{container.Name}::{name}";

    // Resolves the (container module, simple name) for a class/module definition
    // or constant assignment target.
    private (RubyModule container, string name) ResolveConstantTarget(Node path, RubyScope scope)
    {
        switch (path)
        {
            case ConstantReadNode read:
                return (scope.DefinitionTarget ?? _context.ObjectClass, read.Name);
            case ConstantPathNode cpath:
                var container = cpath.Parent is null
                    ? _context.ObjectClass
                    : Eval(cpath.Parent, scope) as RubyModule
                        ?? throw _context.RaiseTypeError("not a class/module");
                return (container, cpath.Name ?? "");
            default:
                throw NotImplemented(path);
        }
    }

    // ---- constant reads --------------------------------------------------

    private object? EvalConstantRead(ConstantReadNode node, RubyScope scope)
    {
        // Lexical nesting first (innermost own tables), then the definition
        // target's ancestors, then Object.
        for (int i = scope.LexicalModules.Count - 1; i >= 0; i--)
            if (scope.LexicalModules[i].TryGetOwnConstant(node.Name, out var lv))
                return lv;

        var target = scope.DefinitionTarget ?? _context.ObjectClass;
        if (target.TryGetConstant(node.Name, out var v)) return v;
        if (_context.ObjectClass.TryGetConstant(node.Name, out var ov)) return ov;

        // CLR interop: a top-level namespace root like `System`.
        if (_context.GetClrNamespaceRoot(node.Name) is { } clrRoot) return clrRoot;

        throw _context.RaiseError(_context.NameErrorClass, $"uninitialized constant {node.Name}");
    }

    private object? EvalConstantPath(ConstantPathNode node, RubyScope scope)
    {
        if (node.Parent is null)
        {
            // ::Name -> top-level
            if (_context.ObjectClass.TryGetConstant(node.Name ?? "", out var tv)) return tv;
            throw _context.RaiseError(_context.NameErrorClass, $"uninitialized constant {node.Name}");
        }

        if (Eval(node.Parent, scope) is not RubyModule parent)
            throw _context.RaiseTypeError("not a class/module");

        // CLR namespace navigation: System::Text::StringBuilder, etc.
        if (parent is ClrNamespaceModule clrNs) return clrNs.Resolve(node.Name ?? "");

        if (parent.TryGetConstant(node.Name ?? "", out var v)) return v;
        throw _context.RaiseError(_context.NameErrorClass,
            $"uninitialized constant {parent.Name}::{node.Name}");
    }

    private object? EvalConstantWrite(ConstantWriteNode node, RubyScope scope)
    {
        var value = Eval(node.Value, scope);
        var target = scope.DefinitionTarget ?? _context.ObjectClass;
        target.SetConstant(node.Name, value);
        NameAnonymousModule(value, target, node.Name);
        return value;
    }

    private object? EvalConstantPathWrite(ConstantPathWriteNode node, RubyScope scope)
    {
        var value = Eval(node.Value, scope);
        if (node.Target is ConstantPathNode cp)
        {
            var container = cp.Parent is null
                ? _context.ObjectClass
                : Eval(cp.Parent, scope) as RubyModule ?? throw _context.RaiseTypeError("not a class/module");
            container.SetConstant(cp.Name ?? "", value);
            NameAnonymousModule(value, container, cp.Name ?? "");
        }
        return value;
    }

    // Assigning an anonymous module/class to a constant names it (Foo = Class.new).
    private static void NameAnonymousModule(object? value, RubyModule container, string name)
    {
        if (value is RubyModule m && m.Name is null)
            m.Name = QualifiedName(container, name);
    }

    // ---- instance / class / global variables -----------------------------

    private object? EvalInstanceVariableRead(InstanceVariableReadNode node, RubyScope scope)
        => GetIvar(scope.Self, node.Name);

    private object? EvalInstanceVariableWrite(InstanceVariableWriteNode node, RubyScope scope)
    {
        var value = Eval(node.Value, scope);
        SetIvar(scope.Self, node.Name, value);
        return value;
    }

    internal object? GetIvar(object? self, string name)
        => self switch
        {
            RubyObject o => o.GetIvar(name),
            RubyModule m => m.GetIvar(name),
            _ => null,
        };

    internal void SetIvar(object? self, string name, object? value)
    {
        switch (self)
        {
            case RubyObject o: o.SetIvar(name, value); break;
            case RubyModule m: m.SetIvar(name, value); break;
            // ivars on immediates are not supported (frozen); silently ignore.
        }
    }

    private object? EvalGlobalVariableRead(GlobalVariableReadNode node, RubyScope scope)
        => _context.GetGlobal(node.Name);

    private object? EvalGlobalVariableWrite(GlobalVariableWriteNode node, RubyScope scope)
    {
        var value = Eval(node.Value, scope);
        _context.SetGlobal(node.Name, value);
        return value;
    }

    private object? EvalClassVariableRead(ClassVariableReadNode node, RubyScope scope)
    {
        var owner = ClassVarOwner(scope);
        foreach (var mod in owner.Ancestors)
            if (mod.ClassVariables.TryGetValue(node.Name, out var v)) return v;
        throw _context.RaiseError(_context.NameErrorClass,
            $"uninitialized class variable {node.Name} in {owner.Name}");
    }

    private object? EvalClassVariableWrite(ClassVariableWriteNode node, RubyScope scope)
    {
        var value = Eval(node.Value, scope);
        var owner = ClassVarOwner(scope);
        // Write to the nearest ancestor already defining it, else the owner.
        foreach (var mod in owner.Ancestors)
        {
            if (mod.ClassVariables.ContainsKey(node.Name)) { mod.ClassVariables[node.Name] = value; return value; }
        }
        owner.ClassVariables[node.Name] = value;
        return value;
    }

    private RubyModule ClassVarOwner(RubyScope scope)
        => scope.DefinitionTarget ?? _context.GetClassOf(scope.Self);

    // ---- defined? --------------------------------------------------------

    private object? EvalDefined(DefinedNode node, RubyScope scope)
    {
        var kind = DefinedKind(node.Value, scope);
        return kind is null ? null : new MutableString(kind);
    }

    private string? DefinedKind(Node value, RubyScope scope)
    {
        try
        {
            switch (value)
            {
                case NilNode: return "expression";
                case TrueNode: return "expression";
                case FalseNode: return "expression";
                case SelfNode: return "self";
                case LocalVariableReadNode n: return scope.HasLocal(n.Name) ? "local-variable" : null;
                case InstanceVariableReadNode n:
                    return HasIvar(scope.Self, n.Name) ? "instance-variable" : null;
                case GlobalVariableReadNode n:
                    return _context.GetGlobal(n.Name) is not null || GlobalDefined(n.Name) ? "global-variable" : null;
                case ClassVariableReadNode n:
                    return ClassVarDefined(scope, n.Name) ? "class-variable" : null;
                case ConstantReadNode n:
                    return ConstantDefined(n.Name, scope) ? "constant" : null;
                case CallNode n:
                    return DefinedCall(n, scope);
                case YieldNode:
                    return FindMethodBlock(scope) is not null ? "yield" : null;
                default:
                    return "expression";
            }
        }
        catch (RubyRaiseException)
        {
            return null;     // evaluation errors mean "not defined"
        }
    }

    private bool HasIvar(object? self, string name)
        => self switch
        {
            RubyObject o => o.TryGetIvar(name, out _),
            RubyModule m => m.TryGetIvar(name, out _),
            _ => false,
        };

    private bool GlobalDefined(string name) => false; // simple: unset globals read as nil

    private bool ClassVarDefined(RubyScope scope, string name)
    {
        var owner = ClassVarOwner(scope);
        foreach (var mod in owner.Ancestors)
            if (mod.ClassVariables.ContainsKey(name)) return true;
        return false;
    }

    private bool ConstantDefined(string name, RubyScope scope)
    {
        for (int i = scope.LexicalModules.Count - 1; i >= 0; i--)
            if (scope.LexicalModules[i].TryGetOwnConstant(name, out _)) return true;
        var target = scope.DefinitionTarget ?? _context.ObjectClass;
        return target.TryGetConstant(name, out _) || _context.ObjectClass.TryGetConstant(name, out _);
    }

    private string? DefinedCall(CallNode node, RubyScope scope)
    {
        object? receiver = node.Receiver is null ? scope.Self : Eval(node.Receiver, scope);
        return _context.RespondTo(receiver, node.Name, includeAll: true) ? "method" : null;
    }
}
