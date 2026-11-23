using System.Collections.Immutable;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Checking;

/// <summary>
/// What a node of a checked expression refers to: the <c>Reference</c> message of <c>checked.proto</c> — the declaration a
/// name resolves to, the overloads a call may take, or the value of an enumeration constant.
/// </summary>
/// <remarks>
/// <para>
/// The nodes that have a reference are those that cel-go gives one: an identifier or a selection chain that resolves to a
/// declared name has the fully qualified name, at the node that names the declaration; a call has the ids of the overloads
/// that type checking found applicable — one, or several when an operand has the type <c>dyn</c> and the evaluation chooses
/// among them — and the resolved name of its function, which cel-go leaves empty and <c>CelCheckedExpr.ToProtobuf</c> writes
/// empty, as cel-go does, because the written tree carries the full name of the function; a message literal has the full name
/// of its type; an enumeration constant has its full name and its value. A field selection has no reference.
/// </para>
/// <para>The reference is immutable and can be shared between threads.</para>
/// </remarks>
public sealed class CelReference
{
    internal CelReference(string name, ImmutableArray<string> overloadIds, CelValue? value)
    {
        Name = name;
        OverloadIds = overloadIds;
        Value = value;
    }

    /// <summary>
    /// The fully qualified name of the declaration: a variable, a type, an enumeration constant, a variable of an enclosing
    /// comprehension or a function, such as <c>acme.sales.limit</c> for <c>limit</c> in the container <c>acme.sales</c> and
    /// <c>math.abs</c> for the call <c>math.abs(x)</c>.
    /// </summary>
    /// <remarks>
    /// The name of an identifier or a selection chain has a leading dot when the expression writes it with a dot and a variable
    /// of an enclosing comprehension has the same first part — <c>.x</c> in <c>[5].exists(x, .x &gt; 0)</c> with a global
    /// <c>x</c> — as the reference of cel-go has it (<c>requiresDisambiguation</c>): the dot tells the planner of cel-go to
    /// take the global variable, not the variable of the comprehension. It is the same name as in the identifier that
    /// <c>CelCheckedExpr.ToProtobuf</c> writes, and <c>ToProtobuf</c> writes <c>name</c> of the reference as it is here.
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// The ids of the overloads of a call that type checking found applicable, in the order of the declarations; empty for a
    /// reference that is not a call. The ids are those of <c>CelOverloadDecl</c>: the ids of cel-go for the standard library and
    /// the extension sets, the ids of the application for its functions.
    /// </summary>
    public ImmutableArray<string> OverloadIds { get; }

    /// <summary>
    /// The value of an enumeration constant, of the kind <see cref="CelValueKind.Int"/>; <see langword="null"/> for any other
    /// reference.
    /// </summary>
    public CelValue? Value { get; }
}
