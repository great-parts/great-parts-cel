using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cel.Expr;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;

namespace Great.Parts.Cel.Eng.Difftest.Compare;

/// <summary>How the outcomes of a node agree: the verdict of a classification of a node on the way down.</summary>
internal enum NodeVerdict
{
    /// <summary>The outcomes agree and are not both errors.</summary>
    Agrees,

    /// <summary>The outcomes agree because both sides give an error, whatever its cause.</summary>
    AgreesInError,

    /// <summary>The outcomes diverge.</summary>
    Diverges,
}

/// <summary>What an instance of the transform of <c>map</c> or <c>transformList</c> gives on both sides, for <see cref="Minimizer.FoldStep"/>.</summary>
internal enum StepKind
{
    /// <summary>Both sides give the same value.</summary>
    Value,

    /// <summary>Both sides give the same unknown parts of the data.</summary>
    Unknown,

    /// <summary>Both sides give an error of evaluation.</summary>
    Error,

    /// <summary>The sides diverge, a side does not compile the instance, or the outcomes agree in another way.</summary>
    Other,
}

/// <summary>
/// Shrinks a divergent case (docs/design/eng-difftest.md, "Outcomes and signatures", "Minimization") by a deterministic
/// descent: while a child of the root still diverges with the same bindings, the first such child in the order of
/// <see cref="Children"/> becomes the case. When no child of the root diverges, the descent looks under each child on which
/// both sides give an error, and under each child that is a list or map literal holding an optional node, which is asked for
/// as its size, in the order of the source, since two errors agree whatever their causes and the size agrees whatever the
/// elements are, so such a child can hide the node that diverges: under it the first child that diverges becomes the case, and when none does, the descent looks
/// in the same way under those of its children on which both sides give an error, depth first, before the next child of the
/// root. A node found so becomes the case without any condition. Under a macro only its range is searched, as the children
/// are those of <see cref="Children"/>. When nothing diverges by these rules and the root is a macro, the first instance of a
/// body of the macro that makes the case smaller and is accepted becomes the case (<see cref="BodyInstances"/>): the body
/// itself when it does not read the iteration variable; else the body with the element in place of the variable for each
/// element of the range (both variables of a macro of two variables), or with a zero literal when the range is empty; then each
/// closed node of the body. When no instance of a body is accepted and the root is <c>map</c> or <c>transformList</c>, the one
/// instance of the step of its fold is tried: the sum of two lists of one instance of the transform each, the first that is
/// unknown and the next that is an error (<see cref="FoldStep"/>). A case is
/// smaller than another when it has fewer macro nodes, or as many and a shorter text. Each node below the root is classified
/// at most once by the descent from a root. The result depends on the case and the outcomes of the sides alone, not on time or
/// on random choices, so the same case gives the same minimized case and the same signature. A failed side ends the
/// minimization with its exception.
/// </summary>
internal static class Minimizer
{
    // The macro of the set of two variables whose fold appends one list element at each step, like map.
    private const string TransformList = "transformList";

    /// <summary>Minimizes a divergent case.</summary>
    /// <param name="divergent">The divergent case.</param>
    /// <param name="classify">
    /// Whether the outcomes of a case diverge, agree, or agree in an error on both sides. Called only for children, nodes under
    /// children that agree in an error, and instances on the way down; at most once for each node of the descent from a root.
    /// </param>
    /// <param name="accepts">
    /// Whether the minimized instance of a body of the macro at the root, which diverges, replaces the root: its arguments are
    /// the root and the instance after its own minimization. <see langword="null"/> means that a macro has no body
    /// instances.
    /// </param>
    /// <param name="rangeValue">
    /// The value that the library gives for a case, <see langword="null"/> when it gives none: it is asked for the range of a
    /// macro that is not a literal, whose elements are those of its value (<see cref="BodyInstances"/>).
    /// <see langword="null"/> means that such a range has no body instances.
    /// </param>
    /// <param name="stepKind">
    /// What an instance of the transform of <c>map</c> or <c>transformList</c> gives on both sides, for the step of the fold
    /// (<see cref="FoldStep"/>); called for the instances of the transform and only when no body instance is accepted.
    /// <see langword="null"/> means that a macro has no instance of the step of its fold.
    /// </param>
    /// <returns>The divergent case whose root has no divergent node under it and no accepted body instance.</returns>
    /// <exception cref="IOException">An <see cref="IOException"/> of <paramref name="classify"/>, <paramref name="accepts"/>, <paramref name="rangeValue"/> or <paramref name="stepKind"/>, after which it is not called again.</exception>
    /// <exception cref="SideFailureException">A <see cref="SideFailureException"/> of <paramref name="classify"/>, <paramref name="accepts"/>, <paramref name="rangeValue"/> or <paramref name="stepKind"/>, after which it is not called again.</exception>
    internal static GeneratedCase Minimize(
        GeneratedCase divergent,
        Func<GeneratedCase, NodeVerdict> classify,
        Func<GeneratedCase, GeneratedCase, bool>? accepts = null,
        Func<GeneratedCase, Value?>? rangeValue = null,
        Func<GeneratedCase, StepKind>? stepKind = null)
    {
        GeneratedCase current = divergent;
        while (Next(current, classify, accepts, rangeValue, stepKind) is { } next)
        {
            current = next;
        }

        return current;
    }

    /// <summary>
    /// The children of a node: the arguments of a call, the elements of a list, the keys and values of a map, the operand of a
    /// selection and of a presence test, and the range of a macro only, since its other arguments are taken as instances of its
    /// bodies (<see cref="BodyInstances"/>).
    /// </summary>
    /// <param name="node">The node.</param>
    internal static IEnumerable<Expr> Children(Expr node) => node switch
    {
        Call call => call.Args,
        ListLiteral list => list.Elements,
        MapLiteral map => map.Entries.SelectMany(entry => new[] { entry.Key, entry.Value }),
        Select select => [select.Operand],
        PresenceTest test => [test.Operand],
        Comprehension comprehension => [comprehension.Range],
        _ => [],
    };

    /// <summary>
    /// The instances of the bodies of a macro, for each argument after the iteration variable, in the order of the source. An
    /// argument that does not read the variable has one instance, the argument itself, whatever the range holds (elements, none
    /// or an error): it does not depend on an element, and the range is not asked. An argument that reads the variable has an
    /// instance for each element of the range, in order (the key for a map): the argument with each free read of the variable
    /// replaced by a node that stands for the element and fits wherever the variable does. For a list or map literal, or such a
    /// literal in
    /// <c>dyn()</c>, the node is the element in parentheses, or <c>dyn(element)</c> when the range is in <c>dyn()</c>, so that
    /// the static types stay; for a literal that is not in <c>dyn()</c> each element that is a string literal has one more
    /// instance, <c>dyn(element)</c>, after those of all the elements, and every element has it when the literal holds literals
    /// of two types, whose join makes the variable <c>dyn</c> (<see cref="ElementInstances"/>). For any other range (a variable, <c>dyn(x1)</c>, <c>lx + []</c>, the result of <c>map</c> or
    /// <c>filter</c>) the elements are those of its value that the library gives: for a list, the index <c>range[i]</c> for
    /// each position <c>i</c> from 0; for a map, <c>dyn(k)</c> for each key <c>k</c> in the order the library iterates the map.
    /// A range whose value is not a list or a map (an error, an unbound variable, a scalar) gives an argument that reads the
    /// variable one instance for each zero literal of the profile, whatever the type of the variable
    /// (<see cref="AnyZeroInstances"/>). A range with no element — a literal without elements, in <c>dyn()</c> or not, or any
    /// other range whose value is an empty list or an empty map — gives an argument that reads the variable one instance for
    /// each zero literal of the profile that fits the static type of the variable (<see cref="ZeroInstances"/>), or, when the
    /// type of the range is not known to the descent (a conditional, a sum, an iteration variable, an empty literal that is not in
    /// <c>dyn()</c>), one instance for each zero literal of the profile (<see cref="AnyZeroInstances"/>). After the
    /// instances of the elements, an argument that reads the variable also has, in the order of the source, one instance for
    /// each closed node of it (<see cref="ClosedNodes"/>): a maximal node that reads no iteration variable of the macros above
    /// it, which is evaluated by itself, once, with the same bindings and without any substitution. A macro of two variables
    /// has the same instances, with both variables replaced at once for each element, by a pair of nodes that stand for the index
    /// or the key and for the element or the value (<see cref="PairReplacements"/>); a body that reads neither variable is itself
    /// the instance, and the range with no element, or whose value is not a list or a map, gives each pair of the zero literals
    /// of the profile. Nothing for a node that is not a macro.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="rangeValue">
    /// The value of the range of the macro on the library, <see langword="null"/> when it gives none. Asked once, and only for a
    /// range that is not a literal and only when the first body that reads the iteration variable is reached.
    /// </param>
    internal static IEnumerable<Expr> BodyInstances(Expr node, Func<Expr, Value?> rangeValue)
    {
        if (node is not Comprehension macro)
        {
            yield break;
        }

        IReadOnlyList<Expr>? replacements = null;
        IReadOnlyList<(Expr First, Expr Second)>? pairs = null;
        foreach (Expr body in macro.Arguments)
        {
            if (macro.SecondVariable is { } second)
            {
                // A two-variable macro: a body that reads neither variable is itself an instance; otherwise there is an instance for
                // each element, with both variables replaced at once (the index or the key, and the element or the value), then each
                // closed node of the body.
                if (!Reads(body, macro.Variable) && !Reads(body, second))
                {
                    yield return body;
                    continue;
                }

                pairs ??= PairReplacements(macro, rangeValue);
                foreach ((Expr first, Expr other) in pairs)
                {
                    yield return Replace(body, new Dictionary<string, Expr> { [macro.Variable] = first, [second] = other });
                }

                foreach (Expr closed in ClosedNodes(body, [macro.Variable, second]))
                {
                    yield return closed;
                }

                continue;
            }

            if (!Reads(body, macro.Variable))
            {
                // Independent of the element: the argument itself, whatever the range holds.
                yield return body;
                continue;
            }

            replacements ??= Replacements(macro, rangeValue);
            foreach (Expr replacement in replacements)
            {
                yield return Replace(body, macro.Variable, replacement);
            }

            foreach (Expr closed in ClosedNodes(body, [macro.Variable]))
            {
                yield return closed;
            }
        }
    }

    /// <summary>
    /// The instance of the step of the fold of <c>map</c> or <c>transformList</c> in which cel-go and the library part:
    /// <c>[a] + [b]</c> with <c>a</c> the first instance of the transform that is unknown on both sides and <c>b</c> the next that
    /// is an error on both. The fold adds <c>[transform]</c> to the accumulator at each element, and cel-go returns the unknown
    /// first operand of a binary function after it has evaluated the second and dropped its error, so an unknown element followed by
    /// an error element gives the unknown, where the library gives the error. The instances of the bodies agree one by one and the
    /// divergence is in their sum alone, so without this instance the case stays the macro. The instances of the transform are the
    /// element of the range in place of the variable, as in <see cref="BodyInstances"/>, but only for a list: a literal of at
    /// least two elements without optional elements, whose nodes are in parentheses, or in <c>dyn()</c> when the range is, and once
    /// more in <c>dyn()</c> when its elements are literals of two types; or any other range whose value is a list of at least two
    /// elements, read as <c>range[i]</c>. A map has no order that cel-go keeps, so it gives none. The elements are taken in
    /// order; one that a predicate of the macro leaves out is skipped, and a predicate that is not a value of <c>bool</c> on the
    /// library (an error, unknown parts, a compile error) gives no instance, since the step of that element is no plain append.
    /// No instance either when an instance of the transform is of another kind than a value, unknown parts or an error on both sides
    /// (it diverges, or a side does not compile it), when an error comes before the first unknown element (both sides give that
    /// error), or when there is no unknown element or no error element after it: a divergence of such an instance is the one of
    /// that case and is not hidden by the sum.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="rangeValue">
    /// The value of a node on the library, <see langword="null"/> when it gives none: of the range, when it is no literal, and of
    /// each predicate.
    /// </param>
    /// <param name="kindOf">What an instance of the transform gives on both sides.</param>
    /// <returns>The instance; <see langword="null"/> for a node that is no <c>map</c> or <c>transformList</c>, and when there is none.</returns>
    internal static Expr? FoldStep(Expr node, Func<Expr, Value?> rangeValue, Func<Expr, StepKind> kindOf)
    {
        if (node is not Comprehension { Macro: ProfileTables.MapMacro or TransformList, Arguments.Count: > 0 } macro)
        {
            return null;
        }

        foreach (IReadOnlyList<IReadOnlyDictionary<string, Expr>> form in ElementForms(macro, rangeValue))
        {
            if (FoldStepOver(macro, form, rangeValue, kindOf) is { } step)
            {
                return step;
            }
        }

        return null;
    }

    // The sum of the first unknown instance of the transform and the next error one over the elements of a form; null when there is
    // none or an instance rules the sum out (FoldStep).
    private static Expr? FoldStepOver(
        Comprehension macro,
        IReadOnlyList<IReadOnlyDictionary<string, Expr>> form,
        Func<Expr, Value?> rangeValue,
        Func<Expr, StepKind> kindOf)
    {
        Expr transform = macro.Arguments[^1];
        List<(StepKind Kind, Expr Body)> steps = [];
        foreach (IReadOnlyDictionary<string, Expr> replacements in form)
        {
            if (Selects(macro, replacements, rangeValue) is not { } selected)
            {
                return null;
            }

            if (!selected)
            {
                continue;
            }

            Expr body = Replace(transform, replacements);
            StepKind kind = kindOf(body);
            if (kind == StepKind.Other)
            {
                return null;
            }

            steps.Add((kind, body));
        }

        // The first error must come after the first unknown element: an error before it is the result of both sides.
        int unknown = steps.FindIndex(step => step.Kind == StepKind.Unknown);
        int error = steps.FindIndex(step => step.Kind == StepKind.Error);
        return unknown >= 0 && error > unknown
            ? new Call(ProfileTables.Add, [new ListLiteral([steps[unknown].Body], TrailingComma: false), new ListLiteral([steps[error].Body], TrailingComma: false)])
            : null;
    }

    // Whether the predicates of the macro, in order, select the element that the replacements stand for: all of them give true on the
    // library; null when one gives no value of bool.
    private static bool? Selects(Comprehension macro, IReadOnlyDictionary<string, Expr> replacements, Func<Expr, Value?> rangeValue)
    {
        bool selected = true;
        foreach (Expr predicate in macro.Arguments.Take(macro.Arguments.Count - 1))
        {
            if (rangeValue(Replace(predicate, replacements)) is not { KindCase: Value.KindOneofCase.BoolValue } outcome)
            {
                return null;
            }

            selected &= outcome.BoolValue;
        }

        return selected;
    }

    // The forms of the elements of the range of a macro, each with the replacements of the variables for every element in order,
    // asked for when the form before it gave no sum; the range of fewer than two elements, a map and a list with optional elements
    // give none (FoldStep).
    private static IEnumerable<IReadOnlyList<IReadOnlyDictionary<string, Expr>>> ElementForms(Comprehension macro, Func<Expr, Value?> rangeValue)
    {
        bool inDyn = macro.Range is Call { Function: ProfileTables.Dyn, Args: [_] };
        Expr literal = macro.Range is Call { Function: ProfileTables.Dyn, Args: [{ } argument] } ? argument : macro.Range;
        return literal switch
        {
            MapLiteral => [],
            ListLiteral list => LiteralForms(macro, list, inDyn),
            _ => ValueForms(macro, rangeValue),
        };
    }

    // The forms of a list literal: its elements in dyn() when the range is, else in parentheses, and once more in dyn() when they are
    // literals of two types. An optional element may leave no element in the list, and the descent does not know which, so a list
    // with one gives none, as a list of fewer than two elements does.
    private static IEnumerable<IReadOnlyList<IReadOnlyDictionary<string, Expr>>> LiteralForms(Comprehension macro, ListLiteral list, bool inDyn)
    {
        if (list.Elements.Count < 2 || list.OptionalPositions.Count > 0)
        {
            yield break;
        }

        if (inDyn)
        {
            yield return LiteralForm(macro, list.Elements, WrapInDyn, WrapInDyn);
            yield break;
        }

        yield return LiteralForm(macro, list.Elements, WrapInParentheses, WrapInParentheses);
        if (HoldsLiteralsOfTwoTypes(list.Elements))
        {
            yield return LiteralForm(macro, list.Elements, WrapInParentheses, WrapInDyn);
        }
    }

    // The form of a range that is no literal: its value on the library is a list of at least two elements.
    private static IEnumerable<IReadOnlyList<IReadOnlyDictionary<string, Expr>>> ValueForms(Comprehension macro, Func<Expr, Value?> rangeValue)
    {
        if (rangeValue(macro.Range) is { KindCase: Value.KindOneofCase.ListValue } value && value.ListValue.Values.Count >= 2)
        {
            yield return IndexedForm(macro, value.ListValue.Values.Count);
        }
    }

    // The replacements for the elements of a list literal: the position and the element, each as the given functions wrap it.
    private static IReadOnlyList<IReadOnlyDictionary<string, Expr>> LiteralForm(
        Comprehension macro, IReadOnlyList<Expr> elements, Func<Expr, Expr> wrapPosition, Func<Expr, Expr> wrapElement) =>
        [.. elements.Select((element, index) => Binding(macro, wrapPosition(Position(index)), wrapElement(element)))];

    // The replacements for the elements of a range that is no literal: the position and range[position].
    private static IReadOnlyList<IReadOnlyDictionary<string, Expr>> IndexedForm(Comprehension macro, int count) =>
        [.. Enumerable.Range(0, count).Select(index => Binding(macro, Position(index), new Call(ProfileTables.Index, [macro.Range, Position(index)])))];

    // The variables of a macro and the nodes that stand for them: the element for the variable of a macro of one variable, the
    // position and the element for the two variables of a macro of two.
    private static Dictionary<string, Expr> Binding(Comprehension macro, Expr position, Expr element) =>
        macro.SecondVariable is { } second
            ? new Dictionary<string, Expr> { [macro.Variable] = position, [second] = element }
            : new Dictionary<string, Expr> { [macro.Variable] = element };

    private static Expr WrapInDyn(Expr node) => new Call(ProfileTables.Dyn, [node]);

    private static Expr WrapInParentheses(Expr node) => node with { Parenthesized = true };

    /// <summary>The number of macro nodes of an expression, the first measure of how small a case is.</summary>
    /// <param name="node">The expression.</param>
    internal static int MacroNodes(Expr node) => node switch
    {
        Comprehension macro => 1 + MacroNodes(macro.Range) + macro.Arguments.Sum(MacroNodes),
        _ => Children(node).Sum(MacroNodes),
    };

    /// <summary>
    /// The next case of the descent from a root: the first node that diverges below it, found by the search under the children
    /// on which both sides give an error (<see cref="FirstDivergent"/>), without any condition; else the first accepted instance
    /// of a body of the macro at the root, which is asked for only if nothing was found; else the instance of the step of the fold
    /// of <c>map</c> or <c>transformList</c> (<see cref="FoldStep"/>), which is asked for only if no instance of a body was
    /// accepted; <see langword="null"/> when there is none.
    /// </summary>
    private static GeneratedCase? Next(
        GeneratedCase current,
        Func<GeneratedCase, NodeVerdict> classify,
        Func<GeneratedCase, GeneratedCase, bool>? accepts,
        Func<GeneratedCase, Value?>? rangeValue,
        Func<GeneratedCase, StepKind>? stepKind)
    {
        if (FirstDivergent(current, classify) is { } found)
        {
            return found;
        }

        if (accepts is null)
        {
            return null;
        }

        // An instance is asked for only when it is smaller than the case: fewer macro nodes, or as many and a shorter text.
        // This is what ends the descent.
        int macros = MacroNodes(current.Expression);
        int length = current.Text.Length;
        Value? ValueOfRange(Expr range) => rangeValue?.Invoke(current with { Expression = range });
        foreach (GeneratedCase instance in BodyInstances(current.Expression, ValueOfRange).Select(body => current with { Expression = body }))
        {
            if (IsSmaller(instance, macros, length) && classify(instance) == NodeVerdict.Diverges)
            {
                GeneratedCase reduced = Minimize(instance, classify, accepts, rangeValue, stepKind);
                if (accepts(current, reduced))
                {
                    return reduced;
                }
            }
        }

        return FoldStepCase(current, classify, accepts, rangeValue, stepKind, macros, length);
    }

    // The instance of the step of the fold, when it makes the case smaller, diverges and is accepted as an instance of a body is.
    private static GeneratedCase? FoldStepCase(
        GeneratedCase current,
        Func<GeneratedCase, NodeVerdict> classify,
        Func<GeneratedCase, GeneratedCase, bool> accepts,
        Func<GeneratedCase, Value?>? rangeValue,
        Func<GeneratedCase, StepKind>? stepKind,
        int macros,
        int length)
    {
        if (stepKind is null)
        {
            return null;
        }

        Func<GeneratedCase, StepKind> kindOfCase = stepKind;
        Value? ValueOfRange(Expr range) => rangeValue?.Invoke(current with { Expression = range });
        if (FoldStep(current.Expression, ValueOfRange, body => kindOfCase(current with { Expression = body })) is not { } step)
        {
            return null;
        }

        GeneratedCase instance = current with { Expression = step };
        if (!IsSmaller(instance, macros, length) || classify(instance) != NodeVerdict.Diverges)
        {
            return null;
        }

        GeneratedCase reduced = Minimize(instance, classify, accepts, rangeValue, stepKind);
        return accepts(current, reduced) ? reduced : null;
    }

    // The first node below a node that diverges: the first child that does, else the first node that does found by the same
    // search under the children on which both sides give an error, and under those that are a list or map literal holding an
    // optional node (HoldsOptional) — its case is its size, which agrees whatever its elements are, so a divergence of an element
    // is hidden by it as by an error — in the order of the source, depth first. The children are classified in order and the
    // search stops at the first that diverges; a node is classified once, and the children that agree in an error, or hold an
    // optional, keep their verdict for the search under them.
    private static GeneratedCase? FirstDivergent(GeneratedCase node, Func<GeneratedCase, NodeVerdict> classify)
    {
        List<GeneratedCase> hiding = [];
        foreach (Expr child in Children(node.Expression))
        {
            GeneratedCase candidate = node with { Expression = child };
            NodeVerdict verdict = classify(candidate);
            if (verdict == NodeVerdict.Diverges)
            {
                return candidate;
            }

            if (verdict == NodeVerdict.AgreesInError || ProfileTables.HoldsOptional(child))
            {
                hiding.Add(candidate);
            }
        }

        foreach (GeneratedCase candidate in hiding)
        {
            if (FirstDivergent(candidate, classify) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static bool IsSmaller(GeneratedCase candidate, int macros, int length)
    {
        int candidateMacros = MacroNodes(candidate.Expression);
        return candidateMacros < macros || (candidateMacros == macros && candidate.Text.Length < length);
    }

    // The nodes that stand for the elements of the range of a macro, in the order of the elements.
    private static IReadOnlyList<Expr> Replacements(Comprehension macro, Func<Expr, Value?> rangeValue)
    {
        // cel.bind: the name stands for its value, which is the one instance.
        if (macro.Macro == ProfileTables.BindMacro)
        {
            return [macro.Range with { Parenthesized = true }];
        }

        // optMap and optFlatMap: the name stands for the held value of the optional, which is range.value(); the range is an optional
        // node, whose value is no range of elements and which no value of the library could be read for.
        if (macro.Macro is ProfileTables.OptMapMacro or ProfileTables.OptFlatMapMacro)
        {
            return [new Call("value", [macro.Range]) { Receiver = true, Overload = "optional_value" }];
        }

        bool inDyn = macro.Range is Call { Function: ProfileTables.Dyn, Args: [_] };
        Expr literal = macro.Range is Call { Function: ProfileTables.Dyn, Args: [{ } argument] } ? argument : macro.Range;
        IReadOnlyList<Expr>? elements = literal switch
        {
            ListLiteral list => ListElements(list),
            MapLiteral map => [.. map.Entries.Select(entry => entry.Key)],
            _ => null,
        };
        if (elements is not null)
        {
            return elements.Count == 0 ? ZeroInstances(macro) : ElementInstances(elements, inDyn);
        }

        // Any other range: the elements are those of its value, which both sides give alike since the range agrees.
        Value? value = rangeValue(macro.Range);
        return value?.KindCase switch
        {
            Value.KindOneofCase.ListValue when value.ListValue.Values.Count == 0 => ZeroInstances(macro),
            Value.KindOneofCase.ListValue =>
                [.. Enumerable.Range(0, value.ListValue.Values.Count)
                    .Select(index => new Call(ProfileTables.Index, [macro.Range, new IntLiteral(index, IntegerForm.DecimalDigits)]))],
            Value.KindOneofCase.MapValue when value.MapValue.Entries.Count == 0 => ZeroInstances(macro),
            Value.KindOneofCase.MapValue =>
                [.. value.MapValue.Entries.Select(entry => KeyLiteral(entry.Key)).OfType<Expr>().Select(key => new Call(ProfileTables.Dyn, [key]))],
            _ => AnyZeroInstances(),
        };
    }

    // The pairs of nodes that stand for the two variables of a two-variable macro, for each element of its range in order: the index
    // and the element of a list, the key and the value of a map. A literal range gives its own nodes (in dyn() each in dyn(), else
    // each in parentheses, then the pairs with a string literal once more with each string literal in dyn()); any other range whose
    // value is a list or a map gives the position and range[position], or dyn(key) and range[dyn(key)] in the order of the library;
    // a range with no element, or whose value is not a list or a map, gives each pair of zero literals of the profile, the first of
    // the pair varying slowest. A pair of other types than the variables does not compile on either side, so the two agree and it is
    // dropped: like the instances of a one-variable macro, it is a guess judged as any instance.
    private static IReadOnlyList<(Expr First, Expr Second)> PairReplacements(Comprehension macro, Func<Expr, Value?> rangeValue)
    {
        bool inDyn = macro.Range is Call { Function: ProfileTables.Dyn, Args: [_] };
        Expr literal = macro.Range is Call { Function: ProfileTables.Dyn, Args: [{ } argument] } ? argument : macro.Range;
        IReadOnlyList<(Expr First, Expr Second)>? entries = literal switch
        {
            ListLiteral list => [.. ListElements(list).Select((element, index) => (Position(index), element))],
            MapLiteral map => [.. map.Entries.Select(entry => (entry.Key, entry.Value))],
            _ => null,
        };
        if (entries is not null)
        {
            return entries.Count == 0 ? ZeroPairs() : ElementPairs(entries, inDyn);
        }

        Value? value = rangeValue(macro.Range);
        return value?.KindCase switch
        {
            Value.KindOneofCase.ListValue when value.ListValue.Values.Count == 0 => ZeroPairs(),
            Value.KindOneofCase.ListValue =>
                [.. Enumerable.Range(0, value.ListValue.Values.Count).Select(index => (Position(index), (Expr)new Call(ProfileTables.Index, [macro.Range, Position(index)])))],
            Value.KindOneofCase.MapValue when value.MapValue.Entries.Count == 0 => ZeroPairs(),
            Value.KindOneofCase.MapValue =>
                [.. value.MapValue.Entries.Select(entry => KeyLiteral(entry.Key)).OfType<Expr>()
                    .Select(key => ((Expr)new Call(ProfileTables.Dyn, [key]), (Expr)new Call(ProfileTables.Index, [macro.Range, new Call(ProfileTables.Dyn, [key])])))],
            _ => ZeroPairs(),
        };
    }

    private static IntLiteral Position(int index) => new(index, IntegerForm.DecimalDigits);

    // The nodes that stand for the elements of a list literal: the element itself, and for an optional element [?e] the value of the
    // optional, e.value(), since e is an optional and the variable of the macro is not (the case 18998813 of seed 20261116:
    // [?optional.of('…')].all(v, matches(v, '(a{32}){32}.')) hid the known divergence of matches). The value of an optional that has none
    // is an error on both sides, so such an instance agrees and is dropped, as a guess of any kind is.
    private static IReadOnlyList<Expr> ListElements(ListLiteral list) =>
    [
        .. list.Elements.Select((element, position) => list.OptionalPositions.Contains(position)
            ? new Call("value", [element]) { Receiver = true, Overload = "optional_value" }
            : element),
    ];

    // The pairs for the elements of a literal range with elements: in dyn() each node in dyn(); otherwise each node in parentheses,
    // then, for each pair that has a string literal, the pair with each string literal in dyn() and the other node in parentheses
    // (the validator of regular expression literals of cel-go reads a string literal that is the first argument of matches as a
    // pattern, as for the instances of a one-variable macro).
    private static IReadOnlyList<(Expr First, Expr Second)> ElementPairs(IReadOnlyList<(Expr First, Expr Second)> entries, bool inDyn)
    {
        static Expr InDyn(Expr node) => new Call(ProfileTables.Dyn, [node]);
        static Expr InParentheses(Expr node) => node with { Parenthesized = true };
        static Expr StringInDyn(Expr node) => node is StringLiteral ? InDyn(node) : InParentheses(node);

        if (inDyn)
        {
            return [.. entries.Select(entry => (InDyn(entry.First), InDyn(entry.Second)))];
        }

        // The keys or the values of literals of two types make that variable dyn, as for a one-variable macro (ElementInstances): the
        // pairs come once more with that node of each pair in dyn().
        bool firstMixed = HoldsLiteralsOfTwoTypes(entries.Select(entry => entry.First));
        bool secondMixed = HoldsLiteralsOfTwoTypes(entries.Select(entry => entry.Second));
        return
        [
            .. entries.Select(entry => (InParentheses(entry.First), InParentheses(entry.Second))),
            .. entries.Where(entry => entry.First is StringLiteral || entry.Second is StringLiteral).Select(entry => (StringInDyn(entry.First), StringInDyn(entry.Second))),
            .. (firstMixed || secondMixed
                ? entries.Select(entry => (firstMixed ? InDyn(entry.First) : InParentheses(entry.First), secondMixed ? InDyn(entry.Second) : InParentheses(entry.Second)))
                : []),
        ];
    }

    // The pairs for a range with no element or with no list or map in it: each pair of the zero literals of the profile.
    private static IReadOnlyList<(Expr First, Expr Second)> ZeroPairs()
    {
        IReadOnlyList<Expr> zeros = AnyZeroInstances();
        return [.. zeros.SelectMany(first => zeros.Select(second => (first, second)))];
    }

    // The nodes that stand for the elements of a literal range with elements. In dyn() each element in dyn(). Otherwise each element
    // in parentheses, then each element that is a string literal in dyn(): the validator of regular expression literals of cel-go
    // reads a string literal that is the first argument of matches as a pattern, so with the subject in parentheses cel-go may
    // refuse the call when the expression is compiled, where the variable it stands for let the evaluation reach the pattern, and
    // the two sides agree; dyn(element) is not a literal for the validator. A literal that holds literals of two types, as the keys
    // of {1u: a, true: b}, has the element type dyn by the join of the checker, so the variable is dyn, and an element in
    // parentheses may not compile where the variable does (mx[?(1u)] for mx a map with int keys, mx[?v] for v dyn): then every
    // element comes once more in dyn(), after those in parentheses (the case 415367 of seed 1304226227 hid the known divergence
    // has(optional_type) we-value under exists_one so).
    private static IReadOnlyList<Expr> ElementInstances(IReadOnlyList<Expr> elements, bool inDyn)
    {
        if (inDyn)
        {
            return [.. elements.Select(element => new Call(ProfileTables.Dyn, [element]))];
        }

        bool mixed = HoldsLiteralsOfTwoTypes(elements);
        return
        [
            .. elements.Select(element => element with { Parenthesized = true }),
            .. elements.Where(element => mixed || element is StringLiteral).Select(element => new Call(ProfileTables.Dyn, [element])),
        ];
    }

    // Whether the nodes hold literals of two types (int, uint, double, bool, string, bytes, null): the join of the checker of two
    // such types is dyn, whatever the other nodes are.
    private static bool HoldsLiteralsOfTwoTypes(IEnumerable<Expr> nodes) =>
        nodes.Select(node => node switch
        {
            IntLiteral => 0,
            UintLiteral => 1,
            DoubleLiteral => 2,
            BoolLiteral => 3,
            StringLiteral => 4,
            BytesLiteral => 5,
            NullLiteral => 6,
            _ => -1,
        }).Where(kind => kind >= 0).Distinct().Skip(1).Any();

    // The nodes that stand for the element of a range whose value is not a list or a map (an error, an unbound variable, a
    // scalar under dyn): one for each zero literal of the profile in the order of ZeroLiteral, whatever the type of the
    // variable, since the range may be any node (an index into a map literal, a conditional) and its type is not known to the
    // descent. An instance of another type than the variable does not compile on either side, so the two agree and it is
    // dropped; it is a guess that is judged like any instance.
    private static IReadOnlyList<Expr> AnyZeroInstances() =>
    [
        .. new[]
        {
            StaticType.Of(ProfileType.StringType),
            StaticType.Of(ProfileType.IntType),
            StaticType.Of(ProfileType.UintType),
            StaticType.Of(ProfileType.DoubleType),
            StaticType.Of(ProfileType.BoolType),
            StaticType.Of(ProfileType.BytesType),
            StaticType.Dyn,
        }.Select(ZeroLiteral).OfType<Expr>(),
    ];

    // The nodes that stand for the element of an empty range, a guess that is judged like any instance: the zero literal that
    // fits the static type of the iteration variable, which is the type of the element of the list or of the key of the map
    // that the range declares: dyn for a range in dyn(), the declared type of a variable of the manifest. A variable of the
    // manifest of another type, or of an element type without a zero literal, has no instance. Any other range — a conditional,
    // a sum, an iteration variable of a macro above, an empty literal that is not in dyn() — has a type that the descent does not
    // know, so each zero literal of the profile is tried, as for a range that is not a list or a map (AnyZeroInstances).
    private static IReadOnlyList<Expr> ZeroInstances(Comprehension macro)
    {
        StaticType? type;
        switch (macro.Range)
        {
            case Call { Function: ProfileTables.Dyn }:
                type = StaticType.Dyn;
                break;
            case Ident ident when ProfileTables.Variables.FirstOrDefault(variable => variable.Name == ident.Name) is { } declared:
                type = declared switch
                {
                    { Type: ProfileType.ListType } => declared.Declared.Element,
                    { Type: ProfileType.MapType } => declared.Declared.Key,
                    { Type: ProfileType.DynType } => StaticType.Dyn,
                    _ => null,
                };
                break;
            default:
                return AnyZeroInstances();
        }

        return type is null || ZeroLiteral(type) is not { } zero ? [] : [zero];
    }

    /// <summary>The zero literal of the profile that fits a static type, <see langword="null"/> for any other type.</summary>
    /// <param name="type">The static type of the iteration variable.</param>
    internal static Expr? ZeroLiteral(StaticType type) => type.Kind switch
    {
        ProfileType.StringType => EmptyString(),
        ProfileType.IntType => new IntLiteral(0, IntegerForm.DecimalDigits),
        ProfileType.UintType => new UintLiteral(0, IntegerForm.DecimalDigits, 'u'),
        ProfileType.DoubleType => new DoubleLiteral(0.0, DoubleForm.Plain),
        ProfileType.BoolType => new BoolLiteral(false),
        ProfileType.BytesType => new BytesLiteral([], 'b', new TextForm(Quote.Apostrophe, null, [])),
        ProfileType.DynType => new Call(ProfileTables.Dyn, [EmptyString()]),
        _ => null,
    };

    private static StringLiteral EmptyString() => new(string.Empty, new TextForm(Quote.Apostrophe, null, []));

    // The maximal nodes of an expression that read none of the variables in scope, in the order of the source: a node that
    // reads one is searched in its children, and in the range and the arguments of a macro, whose own variable is in scope for
    // its arguments. Such a node is evaluated by itself, with the bindings of the case.
    private static IEnumerable<Expr> ClosedNodes(Expr node, IReadOnlyList<string> scope)
    {
        if (!scope.Any(variable => Reads(node, variable)))
        {
            yield return node;
            yield break;
        }

        if (node is Comprehension macro)
        {
            foreach (Expr closed in ClosedNodes(macro.Range, scope))
            {
                yield return closed;
            }

            IReadOnlyList<string> inner = macro.SecondVariable is { } second ? [.. scope, macro.Variable, second] : [.. scope, macro.Variable];
            foreach (Expr closed in macro.Arguments.SelectMany(argument => ClosedNodes(argument, inner)))
            {
                yield return closed;
            }

            yield break;
        }

        foreach (Expr closed in Children(node).SelectMany(child => ClosedNodes(child, scope)))
        {
            yield return closed;
        }
    }

    // The literal of a key of a map: an int, a uint, a bool or a string; null for any other value.
    private static Expr? KeyLiteral(Value key) => key.KindCase switch
    {
        Value.KindOneofCase.Int64Value => new IntLiteral(key.Int64Value, IntegerForm.DecimalDigits),
        Value.KindOneofCase.Uint64Value => new UintLiteral(key.Uint64Value, IntegerForm.DecimalDigits, 'u'),
        Value.KindOneofCase.BoolValue => new BoolLiteral(key.BoolValue),
        Value.KindOneofCase.StringValue => StringKey(key.StringValue),
        _ => null,
    };

    // A string in apostrophes, each code point as itself where it may stand so, else in the first form that fits.
    private static StringLiteral StringKey(string value)
    {
        List<Escape> units = [];
        foreach (Rune rune in value.EnumerateRunes())
        {
            units.Add(TextUnits.StringForms(rune.Value, Quote.Apostrophe, raw: false)[0]);
        }

        return new StringLiteral(value, new TextForm(Quote.Apostrophe, null, units));
    }

    // Whether a node reads a variable that is not shadowed by a macro on the way.
    private static bool Reads(Expr node, string variable) => node switch
    {
        Ident ident => ident.Name == variable,
        Comprehension macro => Reads(macro.Range, variable)
            || (macro.Variable != variable && macro.SecondVariable != variable && macro.Arguments.Any(argument => Reads(argument, variable))),
        _ => Children(node).Any(child => Reads(child, variable)),
    };

    // The node with each free read of the variable replaced; a macro that declares the same variable keeps its arguments.
    private static Expr Replace(Expr node, string variable, Expr replacement) =>
        Replace(node, new Dictionary<string, Expr> { [variable] = replacement });

    // The node with each free read of the variables replaced at once, each by its own node; a macro that declares a variable keeps
    // its arguments free of the replacement of that variable.
    private static Expr Replace(Expr node, IReadOnlyDictionary<string, Expr> replacements) => node switch
    {
        Ident ident when replacements.TryGetValue(ident.Name, out Expr? replacement) => replacement,
        Call call => call with { Args = [.. call.Args.Select(argument => Replace(argument, replacements))] },
        ListLiteral list => list with { Elements = [.. list.Elements.Select(element => Replace(element, replacements))] },
        MapLiteral map => map with
        {
            Entries = [.. map.Entries.Select(entry => entry with { Key = Replace(entry.Key, replacements), Value = Replace(entry.Value, replacements) })],
        },
        Select select => select with { Operand = Replace(select.Operand, replacements) },
        PresenceTest test => test with { Operand = Replace(test.Operand, replacements) },
        Comprehension macro => macro with
        {
            Range = Replace(macro.Range, replacements),
            Arguments = Free(macro, replacements) is { Count: > 0 } inner
                ? [.. macro.Arguments.Select(argument => Replace(argument, inner))]
                : macro.Arguments,
        },
        _ => node,
    };

    // The replacements that reach the arguments of a macro: those of the variables that it does not declare.
    private static Dictionary<string, Expr> Free(Comprehension macro, IReadOnlyDictionary<string, Expr> replacements) =>
        replacements.Where(pair => pair.Key != macro.Variable && pair.Key != macro.SecondVariable).ToDictionary(pair => pair.Key, pair => pair.Value);
}
