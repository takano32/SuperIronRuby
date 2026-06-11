using SuperIronRuby.Parser;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Interpreter;

// Block literals, lambdas, numbered params and `it` (task I8).
public sealed partial class Interpreter
{
    /// <summary>Builds a non-lambda RubyProc from a block literal, closing over
    /// <paramref name="outer"/>.</summary>
    private RubyProc MakeBlockProc(BlockNode node, RubyScope outer)
    {
        var (min, max) = ComputeArity(node.Parameters);
        return new RubyProc(args => RunBlockBody(node.Parameters, node.Body, args, outer, node.NodeId, lambda: false))
        {
            SourceId = node.NodeId,
            ArityMin = min,
            ArityMax = max,
        };
    }

    private object? EvalLambda(LambdaNode node, RubyScope scope)
    {
        var (min, max) = ComputeArity(node.Parameters);
        return new RubyProc(args => RunLambdaBody(node, args, scope))
        {
            IsLambda = true,
            SourceId = node.NodeId,
            ArityMin = min,
            ArityMax = max,
        };
    }

    // Computes (min, max) positional arity from a block/lambda parameter node.
    private static (int min, int max) ComputeArity(Node? parameters)
    {
        if (parameters is BlockParametersNode { Parameters: ParametersNode p })
        {
            int req = p.Requireds.Length + p.Posts.Length;
            bool hasRest = p.Rest is not null;
            int max = hasRest ? -1 : req + p.Optionals.Length;
            return (req, max);
        }
        if (parameters is NumberedParametersNode n) return (n.Maximum, n.Maximum);
        if (parameters is ItParametersNode) return (1, 1);
        return (0, 0);
    }

    private object? RunBlockBody(Node? parameters, Node? body, object?[] args, RubyScope outer,
        long sourceId, bool lambda)
    {
        var blockScope = new RubyScope(ScopeKind.Block, outer)
        {
            CurrentBreakId = sourceId,
        };
        BindBlockParameters(parameters, args, blockScope, lambda ? BindMode.Lambda : BindMode.ProcBlock);

        while (true)
        {
            try
            {
                return Eval(body, blockScope);
            }
            catch (NextUnwind n) { return n.Value; }
            catch (RedoUnwind) { continue; }
        }
    }

    // A lambda runs in a method-like frame so `return`/`break` exit the lambda.
    private object? RunLambdaBody(LambdaNode node, object?[] args, RubyScope outer)
    {
        var scope = new RubyScope(ScopeKind.Block, outer)
        {
            CurrentBreakId = node.NodeId,
        };
        BindBlockParameters(node.Parameters, args, scope, BindMode.Lambda);

        long frame = scope.FrameId;
        try
        {
            return Eval(node.Body, scope);
        }
        catch (ReturnUnwind ret) when (ret.FrameId == frame) { return ret.Value; }
        catch (BreakUnwind br) when (br.SourceId == node.NodeId) { return br.Value; }
    }

    // Binds a block's parameters, handling the three Prism parameter shapes plus
    // numbered params (_1.._9) and `it`.
    private void BindBlockParameters(Node? parameters, object?[] args, RubyScope scope, BindMode mode)
    {
        switch (parameters)
        {
            case null:
                break;
            case BlockParametersNode { Parameters: ParametersNode pnode } bpn:
                BindParameters(pnode, args, scope.Block, scope, mode);
                DeclareBlockLocals(bpn, scope);
                break;
            case BlockParametersNode bpnEmpty:
                DeclareBlockLocals(bpnEmpty, scope);
                break;
            case NumberedParametersNode numbered:
                for (int i = 0; i < numbered.Maximum; i++)
                    scope.DeclareLocal("_" + (i + 1), i < args.Length ? args[i] : null);
                break;
            case ItParametersNode:
                scope.DeclareLocal("it", args.Length > 0 ? args[0] : null);
                break;
        }
    }

    private static void DeclareBlockLocals(BlockParametersNode bpn, RubyScope scope)
    {
        foreach (var local in bpn.Locals)
            if (local is BlockLocalVariableNode bl)
                scope.DeclareLocal(bl.Name, null);   // block-local var, shadows outer
    }

    // `it` used as an implicit single block parameter (Ruby 3.4+) reads the local
    // bound above.
    private object? EvalItLocalVariableRead(ItLocalVariableReadNode node, RubyScope scope)
        => scope.TryGetLocal("it", out var v) ? v : null;
}
