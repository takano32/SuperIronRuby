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
        var topScope = scope ?? CreateTopLevelScope();
        try
        {
            return Eval(unit.Root, topScope);
        }
        catch (ReturnUnwind ret) when (ret.FrameId == topScope.FrameId)
        {
            // top-level `return` ends the script with the returned value
            return ret.Value;
        }
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

            // -- calls & boolean operators (Interpreter.Calls.cs) --
            case CallNode n: return EvalCall(n, scope);
            case AndNode n: return EvalAnd(n, scope);
            case OrNode n: return EvalOr(n, scope);

            // -- definitions (Interpreter.Definitions.cs) --
            case DefNode n: return EvalDef(n, scope);
            case ReturnNode n: return EvalReturn(n, scope);
            case YieldNode n: return EvalYield(n, scope);
            case SuperNode n: return EvalSuper(n, scope);
            case ForwardingSuperNode n: return EvalForwardingSuper(n, scope);

            // -- classes/modules/constants/variables (Interpreter.Classes.cs) --
            case ClassNode n: return EvalClass(n, scope);
            case ModuleNode n: return EvalModule(n, scope);
            case SingletonClassNode n: return EvalSingletonClass(n, scope);
            case ConstantReadNode n: return EvalConstantRead(n, scope);
            case ConstantPathNode n: return EvalConstantPath(n, scope);
            case ConstantWriteNode n: return EvalConstantWrite(n, scope);
            case ConstantPathWriteNode n: return EvalConstantPathWrite(n, scope);
            case InstanceVariableReadNode n: return EvalInstanceVariableRead(n, scope);
            case InstanceVariableWriteNode n: return EvalInstanceVariableWrite(n, scope);
            case GlobalVariableReadNode n: return EvalGlobalVariableRead(n, scope);
            case GlobalVariableWriteNode n: return EvalGlobalVariableWrite(n, scope);
            case ClassVariableReadNode n: return EvalClassVariableRead(n, scope);
            case ClassVariableWriteNode n: return EvalClassVariableWrite(n, scope);
            case DefinedNode n: return EvalDefined(n, scope);

            // -- control flow (Interpreter.ControlFlow.cs) --
            case IfNode n: return EvalIf(n, scope);
            case UnlessNode n: return EvalUnless(n, scope);
            case ElseNode n: return EvalElse(n, scope);
            case WhileNode n: return EvalWhile(n, scope);
            case UntilNode n: return EvalUntil(n, scope);
            case CaseNode n: return EvalCase(n, scope);
            case BreakNode n: return EvalBreak(n, scope);
            case NextNode n: return EvalNext(n, scope);
            case RedoNode n: return EvalRedo(n, scope);
            case BeginNode n: return EvalBegin(n, scope);
            case RescueModifierNode n: return EvalRescueModifier(n, scope);
            case ForNode n: return EvalFor(n, scope);
            case RetryNode n: return EvalRetry(n, scope);

            // -- composite literals (Interpreter.Strings.cs) --
            case InterpolatedStringNode n: return EvalInterpolatedString(n, scope);
            case InterpolatedSymbolNode n: return EvalInterpolatedSymbol(n, scope);
            case EmbeddedStatementsNode n: return EvalEmbeddedStatements(n, scope);
            case ArrayNode n: return EvalArray(n, scope);
            case HashNode n: return EvalHash(n, scope);
            case RangeNode n: return EvalRange(n, scope);
            case RegularExpressionNode n: return EvalRegexp(n, scope);
            case InterpolatedRegularExpressionNode n: return EvalInterpolatedRegexp(n, scope);

            // -- assignments (Interpreter.Assignments.cs) --
            case MultiWriteNode n: return EvalMultiWrite(n, scope);
            case LocalVariableOrWriteNode n: return EvalLocalOrWrite(n, scope);
            case LocalVariableAndWriteNode n: return EvalLocalAndWrite(n, scope);
            case LocalVariableOperatorWriteNode n: return EvalLocalOpWrite(n, scope);
            case InstanceVariableOrWriteNode n: return EvalIvarOrWrite(n, scope);
            case InstanceVariableAndWriteNode n: return EvalIvarAndWrite(n, scope);
            case InstanceVariableOperatorWriteNode n: return EvalIvarOpWrite(n, scope);
            case GlobalVariableOrWriteNode n: return EvalGvarOrWrite(n, scope);
            case GlobalVariableAndWriteNode n: return EvalGvarAndWrite(n, scope);
            case GlobalVariableOperatorWriteNode n: return EvalGvarOpWrite(n, scope);
            case ClassVariableOrWriteNode n: return EvalCvarOrWrite(n, scope);
            case ClassVariableAndWriteNode n: return EvalCvarAndWrite(n, scope);
            case ClassVariableOperatorWriteNode n: return EvalCvarOpWrite(n, scope);
            case ConstantOrWriteNode n: return EvalConstOrWrite(n, scope);
            case ConstantOperatorWriteNode n: return EvalConstOpWrite(n, scope);
            case CallOrWriteNode n: return EvalCallOrWrite(n, scope);
            case CallAndWriteNode n: return EvalCallAndWrite(n, scope);
            case CallOperatorWriteNode n: return EvalCallOpWrite(n, scope);
            case IndexOrWriteNode n: return EvalIndexOrWrite(n, scope);
            case IndexAndWriteNode n: return EvalIndexAndWrite(n, scope);
            case IndexOperatorWriteNode n: return EvalIndexOpWrite(n, scope);

            // -- blocks/lambdas (Interpreter.Blocks.cs) --
            case LambdaNode n: return EvalLambda(n, scope);
            case ItLocalVariableReadNode n: return EvalItLocalVariableRead(n, scope);

            // -- pattern matching (Interpreter.Patterns.cs) --
            case CaseMatchNode n: return EvalCaseMatch(n, scope);
            case MatchPredicateNode n: return EvalMatchPredicate(n, scope);
            case MatchRequiredNode n: return EvalMatchRequired(n, scope);

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
