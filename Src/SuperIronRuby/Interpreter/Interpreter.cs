using System.Numerics;
using SuperIronRuby.Parser;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Interpreter;

/// <summary>
/// A tree-walking interpreter over the Prism AST. The evaluator is a
/// <c>partial class</c> split by node family (literals, calls, definitions,
/// control flow, ...). This file holds the driver: the top-level
/// <see cref="Run"/> entry point and the central <see cref="Eval"/> dispatch.
/// </summary>
public sealed partial class Interpreter
{
    private readonly RubyContext _context;
    private ParseResult? _unit;

    public RubyContext Context => _context;

    public Interpreter(RubyContext context)
    {
        _context = context;
        // The real Ruby-method invoker is installed by task I3; until then a
        // stub keeps Send working for builtins and fails loudly for Ruby methods.
        _context.InterpretedInvoker ??= (_, _, _, _) =>
            throw new NotImplementedException("interpreted method invocation arrives in task I3");
    }

    /// <summary>Executes a parsed unit at the top level (or in a supplied scope).</summary>
    public object? Run(ParseResult unit, RubyScope? scope = null)
    {
        _unit = unit;
        scope ??= CreateTopLevelScope();
        return Eval(unit.Root, scope);
    }

    /// <summary>Creates the top-level scope (self = the main object, constants and
    /// methods defined on Object).</summary>
    public RubyScope CreateTopLevelScope()
    {
        var scope = new RubyScope(ScopeKind.TopLevel, null)
        {
            Self = _context.MainObject,
            DefinitionTarget = _context.ObjectClass,
        };
        scope.LexicalModules.Add(_context.ObjectClass);
        return scope;
    }

    /// <summary>Evaluates a node to a Ruby value. Unhandled node kinds throw
    /// <see cref="NotImplemented"/>. Cases are added by the per-family partials.</summary>
    internal object? Eval(Node? node, RubyScope scope)
    {
        switch (node)
        {
            case null:
                return null;

            // -- literals & statements (Interpreter.Literals.cs) --
            case ProgramNode n: return Eval(n.Statements, scope);
            case StatementsNode n: return EvalStatements(n, scope);
            case ParenthesesNode n: return EvalParentheses(n, scope);
            case IntegerNode n: return n.Value;            // long or BigInteger
            case FloatNode n: return n.Value;
            case TrueNode: return true;
            case FalseNode: return false;
            case NilNode: return null;
            case SelfNode: return scope.Self;
            case StringNode n: return EvalString(n, scope);
            case SymbolNode n: return _context.Intern(n.Unescaped);
            case SourceFileNode: return new MutableString(_unit?.FilePath ?? "(eval)");
            case SourceLineNode n: return (long)(_unit?.LineForOffset(n.Location.StartOffset) ?? 1);
            case SourceEncodingNode: return new MutableString("UTF-8");

            // -- locals (Interpreter.Literals.cs) --
            case LocalVariableReadNode n: return EvalLocalRead(n, scope);
            case LocalVariableWriteNode n: return EvalLocalWrite(n, scope);

            default:
                throw NotImplemented(node);
        }
    }

    /// <summary>Helper for an as-yet-unimplemented node, with source line info.</summary>
    internal NotSupportedException NotImplemented(Node node)
    {
        int line = _unit?.LineForOffset(node.Location.StartOffset) ?? 0;
        return new NotSupportedException(
            $"SuperIronRuby: node {node.Type} not yet supported (at line {line})");
    }

    /// <summary>Ruby truthiness (everything but nil/false is truthy).</summary>
    internal static bool Truthy(object? value) => RubyContext.Truthy(value);
}
