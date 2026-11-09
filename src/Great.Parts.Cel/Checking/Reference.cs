using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Checking;

// What an identifier, a selection resolved as a name or a call refers to: a declared variable, a type
// identifier of the standard library version or an object type of the application, a constant of an enumeration
// type, a variable of an enclosing comprehension, the field of an object type that a selection or a presence test
// reads, or the overload candidates of a call in the order of the declaration index.
internal sealed class Reference
{
    private Reference(
        CelVariableDecl? variable,
        CelType? typeValue,
        string? local,
        ImmutableArray<OverloadBinding> candidates,
        CelObjectType? objectType = null,
        int fieldNumber = -1,
        long? constant = null)
    {
        Variable = variable;
        TypeValue = typeValue;
        Local = local;
        Candidates = candidates;
        ObjectType = objectType;
        FieldNumber = fieldNumber;
        Constant = constant;
    }

    // The declared variable of an identifier or of a selection chain resolved as its qualified name; null
    // otherwise.
    internal CelVariableDecl? Variable { get; }

    // The type that a type identifier, such as int, stands for: its type value; null otherwise.
    internal CelType? TypeValue { get; }

    // The name of the iteration variable or the accumulator of an enclosing comprehension that an
    // identifier reads; null otherwise.
    internal string? Local { get; }

    // The candidates of a call; empty for a name. A call with a target whose candidates are global
    // overloads is the call of a qualified function name, such as a.b.f(x): the target is part of the name
    // and only the arguments are passed.
    internal ImmutableArray<OverloadBinding> Candidates { get; }

    // The description of the object type whose field a selection or a presence test reads; null otherwise.
    internal CelObjectType? ObjectType { get; }

    // The number of that field; -1 otherwise.
    internal int FieldNumber { get; }

    // The int value of an enumeration constant that an identifier or a selection chain names; null otherwise.
    internal long? Constant { get; }

    internal static Reference ToVariable(CelVariableDecl variable) => new(variable, null, null, []);

    internal static Reference ToTypeIdentifier(CelType typeValue) => new(null, typeValue, null, []);

    internal static Reference ToLocal(string name) => new(null, null, name, []);

    internal static Reference ToField(CelObjectType objectType, int number) => new(null, null, null, [], objectType, number);

    internal static Reference ToConstant(long value) => new(null, null, null, [], constant: value);

    internal static Reference ToCandidates(ImmutableArray<OverloadBinding> candidates) => new(null, null, null, candidates);
}
