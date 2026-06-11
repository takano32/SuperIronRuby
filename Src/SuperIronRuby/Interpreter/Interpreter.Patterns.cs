using SuperIronRuby.Parser;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Interpreter;

// case/in pattern matching (task I9).
public sealed partial class Interpreter
{
    private object? EvalCaseMatch(CaseMatchNode node, RubyScope scope)
    {
        var subject = node.Predicate is null ? null : Eval(node.Predicate, scope);

        foreach (var cond in node.Conditions)
        {
            if (cond is not InNode inNode) continue;

            // A guard is parsed as an If/Unless wrapping the body's pattern.
            var (pattern, guard, guardNegated) = SplitGuard(inNode.Pattern);
            if (!MatchPattern(pattern, subject, scope)) continue;
            if (guard is not null)
            {
                bool ok = Truthy(Eval(guard, scope));
                if (guardNegated) ok = !ok;
                if (!ok) continue;
            }
            return Eval(inNode.Statements, scope);
        }

        if (node.ElseClause is ElseNode els) return Eval(els.Statements, scope);
        throw _context.RaiseError(_context.NoMatchingPatternErrorClass, _context.Inspect(subject));
    }

    // `in pattern if g` / `in pattern unless g` arrive as an If/Unless node whose
    // predicate is the guard and whose statements are the pattern.
    private static (Node pattern, Node? guard, bool negated) SplitGuard(Node patternNode)
        => patternNode switch
        {
            IfNode i when i.Statements is not null => (FirstStatement(i.Statements), i.Predicate, false),
            UnlessNode u when u.Statements is not null => (FirstStatement(u.Statements), u.Predicate, true),
            _ => (patternNode, null, false),
        };

    private static Node FirstStatement(Node stmts)
        => stmts is StatementsNode { Body.Length: > 0 } s ? s.Body[0] : stmts;

    /// <summary>Boolean pattern match (`x in pat`).</summary>
    private object? EvalMatchPredicate(MatchPredicateNode node, RubyScope scope)
        => MatchPattern(node.Pattern, Eval(node.Value, scope), scope);

    /// <summary>Rightward match (`x => pat`): binds or raises.</summary>
    private object? EvalMatchRequired(MatchRequiredNode node, RubyScope scope)
    {
        var value = Eval(node.Value, scope);
        if (!MatchPattern(node.Pattern, value, scope))
            throw _context.RaiseError(_context.NoMatchingPatternErrorClass, _context.Inspect(value));
        return null;
    }

    // The matcher. Binds captured variables in `scope` as it goes.
    private bool MatchPattern(Node pattern, object? value, RubyScope scope)
    {
        switch (pattern)
        {
            case LocalVariableTargetNode capture:
                scope.SetLocal(capture.Name, value);   // a bare name always binds
                return true;

            case CapturePatternNode cap:
                if (!MatchPattern(cap.Value, value, scope)) return false;
                AssignTarget(cap.Target, value, scope);
                return true;

            case AlternationPatternNode alt:
                return MatchPattern(alt.Left, value, scope) || MatchPattern(alt.Right, value, scope);

            case PinnedVariableNode pin:
                return Truthy(_context.Send(Eval(pin.Variable, scope), "===", new[] { value }));

            case PinnedExpressionNode pex:
                return Truthy(_context.Send(Eval(pex.Expression, scope), "===", new[] { value }));

            case ArrayPatternNode arr:
                return MatchArrayPattern(arr, value, scope);

            case FindPatternNode find:
                return MatchFindPattern(find, value, scope);

            case HashPatternNode hash:
                return MatchHashPattern(hash, value, scope);

            case NilNode:
                return value is null;
            case TrueNode:
                return value is true;
            case FalseNode:
                return value is false;

            default:
                // Any other expression is a value pattern: `pat === value`.
                var patVal = Eval(pattern, scope);
                return Truthy(_context.Send(patVal, "===", new[] { value }));
        }
    }

    private bool MatchArrayPattern(ArrayPatternNode pattern, object? value, RubyScope scope)
    {
        if (pattern.Constant is not null)
        {
            if (!Truthy(_context.Send(Eval(pattern.Constant, scope), "===", new[] { value }))) return false;
        }

        var array = Deconstruct(value);
        if (array is null) return false;

        int req = pattern.Requireds.Length;
        int post = pattern.Posts.Length;
        bool hasRest = pattern.Rest is not null;

        if (hasRest)
        {
            if (array.Count < req + post) return false;
        }
        else
        {
            if (array.Count != req + post) return false;
        }

        for (int i = 0; i < req; i++)
            if (!MatchPattern(pattern.Requireds[i], array[i], scope)) return false;

        if (hasRest)
        {
            int restCount = array.Count - req - post;
            if (pattern.Rest is SplatNode { Expression: { } restTarget })
            {
                var restArr = new RubyArray();
                for (int i = 0; i < restCount; i++) restArr.Add(array[req + i]);
                if (!MatchPattern(restTarget, restArr, scope)) return false;
            }
            for (int i = 0; i < post; i++)
                if (!MatchPattern(pattern.Posts[i], array[req + restCount + i], scope)) return false;
        }

        return true;
    }

    private bool MatchFindPattern(FindPatternNode pattern, object? value, RubyScope scope)
    {
        var array = Deconstruct(value);
        if (array is null) return false;
        int mid = pattern.Requireds.Length;

        for (int start = 0; start + mid <= array.Count; start++)
        {
            bool ok = true;
            for (int i = 0; i < mid; i++)
                if (!MatchPattern(pattern.Requireds[i], array[start + i], scope)) { ok = false; break; }
            if (!ok) continue;

            if (pattern.Left is SplatNode { Expression: { } lt })
            {
                var pre = new RubyArray();
                for (int i = 0; i < start; i++) pre.Add(array[i]);
                MatchPattern(lt, pre, scope);
            }
            if (pattern.Right is SplatNode { Expression: { } rt })
            {
                var postArr = new RubyArray();
                for (int i = start + mid; i < array.Count; i++) postArr.Add(array[i]);
                MatchPattern(rt, postArr, scope);
            }
            return true;
        }
        return false;
    }

    private bool MatchHashPattern(HashPatternNode pattern, object? value, RubyScope scope)
    {
        if (pattern.Constant is not null)
        {
            if (!Truthy(_context.Send(Eval(pattern.Constant, scope), "===", new[] { value }))) return false;
        }

        var hash = DeconstructKeys(value);
        if (hash is null) return false;

        var matchedKeys = new HashSet<object?>();
        foreach (var element in pattern.Elements)
        {
            if (element is not AssocNode assoc) continue;
            var key = Eval(assoc.Key, scope);
            matchedKeys.Add(key);
            if (!hash.TryGetValue(key, out var v)) return false;
            // `key:` shorthand binds the value to a local of the key's name.
            if (assoc.Value is null or ImplicitNode)
            {
                if (key is RubySymbol sym) scope.SetLocal(sym.Name, v);
            }
            else if (!MatchPattern(assoc.Value, v, scope))
            {
                return false;
            }
        }

        if (pattern.Rest is AssocSplatNode { Value: { } restTarget })
        {
            var rest = new RubyHash();
            foreach (var e in hash.Entries())
                if (!matchedKeys.Contains(e.Key)) rest.Store(e.Key, e.Value);
            MatchPattern(restTarget, rest, scope);
        }
        return true;
    }

    // Returns the value as a RubyArray (via #deconstruct for user objects).
    private RubyArray? Deconstruct(object? value)
    {
        if (value is RubyArray a) return a;
        if (_context.RespondTo(value, "deconstruct", includeAll: true))
            return _context.Send(value, "deconstruct", System.Array.Empty<object?>()) as RubyArray;
        return null;
    }

    // Returns the value as a RubyHash (via #deconstruct_keys for user objects).
    private RubyHash? DeconstructKeys(object? value)
    {
        if (value is RubyHash h) return h;
        if (_context.RespondTo(value, "deconstruct_keys", includeAll: true))
            return _context.Send(value, "deconstruct_keys", new object?[] { null }) as RubyHash;
        return null;
    }
}
