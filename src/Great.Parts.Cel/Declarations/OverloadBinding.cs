namespace Great.Parts.Cel.Declarations;

// One overload of an environment: its declaration with the code that evaluation calls and the cost of
// a call. Type checking records bindings as the candidates of a call, and evaluation calls them.
internal sealed class OverloadBinding
{
    internal OverloadBinding(
        string function,
        CelOverloadDecl declaration,
        OverloadImplementation? implementation,
        ulong fixedCost,
        CelCostFunction? argumentCost,
        string source)
    {
        Function = function;
        Declaration = declaration;
        Implementation = implementation;
        FixedCost = fixedCost;
        ArgumentCost = argumentCost;
        Source = source;
    }

    // The name of the function in the syntax tree, such as _+_.
    internal string Function { get; }

    internal CelOverloadDecl Declaration { get; }

    // Null for a special form that the planner evaluates itself, such as _||_.
    internal OverloadImplementation? Implementation { get; }

    // The cost of a call in units: the fixed part, and the part computed from the arguments, if any.
    internal ulong FixedCost { get; }

    internal CelCostFunction? ArgumentCost { get; }

    // The source that build errors name, such as "standard library v1".
    internal string Source { get; }
}
