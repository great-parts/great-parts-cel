using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Checking;

/// <summary>
/// A type-checked expression: the <c>CheckedExpr</c> message of <c>checked.proto</c> — the parsed expression with the CEL
/// type of every node and what each name and call refers to.
/// </summary>
/// <remarks>
/// <para>
/// <c>CelValidationResult.CheckedExpr</c> gives it for an expression valid in an environment. A tool reads the type of each
/// node and the declarations and overloads the expression uses; an application stores it with <see cref="ToProtobuf"/> or
/// passes it to a service that reads <c>CheckedExpr</c>, such as one built on cel-go. A stored checked expression compiles
/// again from its parsed part — <c>CelParsedExpr.FromCheckedExprProtobuf</c>, then
/// <c>CelEnvironment.CompileParsed</c> — which type-checks it in the environment of the call.
/// </para>
/// <para>
/// The maps are those of the type checking of this library, by the same rules as cel-go: the types follow the CEL language
/// definition; a type that type checking narrows in a later version by a fix of conformance to cel-spec can change in a minor
/// version, as the result of an expression can (see the release notes).
/// </para>
/// <para>The checked expression is immutable and can be shared between threads.</para>
/// </remarks>
public sealed class CelCheckedExpr
{
    // The maps are made on the first read and published once; two threads can make them at the same time, and both are equal.
    private IReadOnlyDictionary<int, CelType>? _typeMap;
    private IReadOnlyDictionary<int, CelReference>? _referenceMap;

    internal CelCheckedExpr(CheckedExpression expression)
    {
        Expression = expression;
    }

    /// <summary>
    /// The result of the check that this expression wraps, with the references of the check, which the encoder of protobuf reads.
    /// </summary>
    internal CheckedExpression Expression { get; }

    /// <summary>
    /// The parsed expression that was checked: the same tree, with the same ids, as type checking read, not rewritten.
    /// </summary>
    /// <remarks>
    /// Unlike the AST of cel-go after type checking, the tree keeps the form of parsing: <c>acme.limit</c> stays a selection
    /// over the identifier <c>acme</c>, <c>math.abs(x)</c> stays a call with the target <c>math</c>; what they resolve to is in
    /// <see cref="ReferenceMap"/>. <see cref="ToProtobuf"/> writes the form of cel-go.
    /// </remarks>
    public CelParsedExpr ParsedExpr => Expression.Parsed;

    /// <summary>
    /// The CEL type of the whole expression: the type of the root in <see cref="TypeMap"/>.
    /// </summary>
    public CelType ResultType => Expression.ResultType;

    /// <summary>
    /// The CEL type of the nodes of <see cref="ParsedExpr"/>, by the id of the node: <c>type_map</c> of <c>checked.proto</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every node has a type, <c>dyn</c> for a node whose type is known only at evaluation, except the nodes inside a resolved
    /// name, which type checking does not check and cel-go removes from its tree: the identifier and the selections of a chain
    /// below the node that names a declaration (<c>acme</c> and <c>acme.Color</c> in <c>acme.Color.RED</c>) and the target of
    /// a call of a qualified function name (<c>math</c> in <c>math.abs(x)</c>). Entries of maps and messages are not nodes and
    /// have no type.
    /// </para>
    /// </remarks>
    public IReadOnlyDictionary<int, CelType> TypeMap
    {
        get
        {
            IReadOnlyDictionary<int, CelType>? map = Volatile.Read(ref _typeMap);
            if (map is null)
            {
                map = new ReadOnlyDictionary<int, CelType>(new Dictionary<int, CelType>(Expression.Types));
                Volatile.Write(ref _typeMap, map);
            }

            return map;
        }
    }

    /// <summary>
    /// The references of the identifiers, selection chains, calls and message literals of <see cref="ParsedExpr"/>, by the id
    /// of the node: <c>reference_map</c> of <c>checked.proto</c>.
    /// </summary>
    /// <remarks>
    /// A name written as a selection chain has its reference at the node that names the declaration, the outer selection of
    /// <c>acme.Color.RED</c>; a call of a qualified function name, such as <c>math.abs(x)</c>, has it at the call. The name of
    /// a reference that a comprehension variable would shadow keeps the leading dot (see <see cref="CelReference.Name"/>).
    /// </remarks>
    public IReadOnlyDictionary<int, CelReference> ReferenceMap
    {
        get
        {
            IReadOnlyDictionary<int, CelReference>? map = Volatile.Read(ref _referenceMap);
            if (map is null)
            {
                map = CreateReferenceMap(Expression.References);
                Volatile.Write(ref _referenceMap, map);
            }

            return map;
        }
    }

    // The references of the check as the public ones: a field of an object type, which cel-go records no reference for, is left out.
    private static ReadOnlyDictionary<int, CelReference> CreateReferenceMap(IReadOnlyDictionary<int, Reference> references)
    {
        var map = new Dictionary<int, CelReference>(references.Count);
        foreach (KeyValuePair<int, Reference> entry in references)
        {
            if (!entry.Value.IsField)
            {
                map.Add(entry.Key, ToPublic(entry.Value));
            }
        }

        return new ReadOnlyDictionary<int, CelReference>(map);
    }

    private static CelReference ToPublic(Reference reference)
    {
        if (reference.IdentifierName is { } identifier)
        {
            return new CelReference(identifier, [], reference.Constant is { } constant ? CelValue.FromInt64(constant) : null);
        }

        if (reference.Local is { } local)
        {
            return new CelReference(local, [], null);
        }

        if (reference.ObjectType is { } objectType)
        {
            return new CelReference(objectType.Name, [], null);
        }

        ImmutableArray<string>.Builder overloadIds = ImmutableArray.CreateBuilder<string>(reference.Candidates.Length);
        foreach (OverloadBinding candidate in reference.Candidates)
        {
            overloadIds.Add(candidate.Declaration.OverloadId);
        }

        string function = reference.Candidates.IsEmpty ? string.Empty : reference.Candidates[0].Function;
        return new CelReference(function, overloadIds.MoveToImmutable(), null);
    }

    /// <summary>
    /// Writes the checked expression as the binary protobuf form of the message <c>cel.expr.CheckedExpr</c> of cel-spec, which
    /// cel-go and other implementations read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The library writes the message itself, without a protobuf package, by the fields of <c>checked.proto</c> of cel-expr
    /// v0.25.1. <c>expr</c> is the tree in the form that type checking in cel-go gives, which the planner of cel-go reads: an
    /// identifier or a selection chain that resolves to a declaration becomes an identifier with the fully qualified name, such
    /// as <c>acme.limit</c>; a call of a qualified function name becomes a global call of the full name without a target, such
    /// as <c>math.abs(x)</c>; a global call and a message literal get the full name that the container resolves. The ids of the
    /// remaining nodes are those of <see cref="ParsedExpr"/>; the nodes removed by the rewriting are not written, nor are their
    /// positions. A name that a comprehension variable would shadow keeps its leading dot, in the identifier and in <c>name</c>
    /// of its reference. The macro calls are written in the form of parsing, not rewritten: cel-go copies only the calls and
    /// lists among the arguments of a macro call and rewrites the other nodes of the arguments with the tree, so its
    /// <c>macro_calls</c> can differ from these in names (<c>acme.limit</c> where the parsing has <c>limit</c>) — a difference
    /// of the form of the data, as the planner of cel-go does not read <c>macro_calls</c>. <c>type_map</c> and <c>reference_map</c> have the
    /// entries of <see cref="TypeMap"/> and <see cref="ReferenceMap"/>, and the reference of a call has an empty name, as
    /// cel-go writes it.
    /// </para>
    /// <para>
    /// A <c>decimal</c> constant is written as the call <c>decimal("…")</c> of the overload <c>string_to_decimal</c>, as
    /// <c>CelParsedExpr.ToProtobuf</c> writes it. A type of this library that <c>checked.proto</c> has no case for is written as
    /// the abstract type of its name, as cel-go writes <c>optional_type</c>: <c>decimal</c> is the abstract type <c>decimal</c>.
    /// </para>
    /// <para>
    /// <c>CelParsedExpr.FromCheckedExprProtobuf</c> loads the rewritten tree, and <c>CelEnvironment.CompileParsed</c> compiles
    /// it to the same result as the text in the same environment, except for a name that the container resolves to another
    /// declaration when the tree is checked again: a full name such as <c>acme.limit</c> beside <c>acme.acme.limit</c> in the
    /// container <c>acme</c>, and a name that the text wrote with a leading dot and that is written without it, as cel-go
    /// does, such as <c>.y</c> beside <c>com.example.y</c> in the container <c>com.example</c>.
    /// </para>
    /// <para>The same message in the package <c>google.api.expr.v1alpha1</c> has the same fields and reads the same bytes.</para>
    /// </remarks>
    /// <returns>The bytes of the message.</returns>
    /// <exception cref="InvalidOperationException">The tree is too deep to be written for the stack that is left.</exception>
    public byte[] ToProtobuf() => CheckedExprCodec.Write(this);
}
