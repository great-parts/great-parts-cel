using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Cel.Expr;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Type = Cel.Expr.Type;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>
/// The functions that a case declares in <c>type_env</c> as one internal extension set of the runner: an overload for each
/// <c>overload</c> of each <c>function</c>, with an implementation that fails, and an abstract type for each name of an
/// <c>abstract_type</c> that the declarations use. Only a case that checks types declares functions, so no implementation is called;
/// <c>Build</c> asks for one for each overload and no public member declares an abstract type of the application.
/// </summary>
internal sealed class CaseDeclarations : ExtensionDefinition
{
    /// <summary>The name of the set.</summary>
    internal const string SetName = "type_env";

    /// <summary>The text of the exception of the implementation of every overload.</summary>
    internal const string StubMessage = "function declared by a case that only checks types";

    private const string SourceText = "type_env of the case";

    // The implementation of every overload: a case that evaluates what it declared fails, never passes; the library reports the exception
    // as an error of the call.
    private static readonly OverloadImplementation Stub = static (ReadOnlySpan<CelValue> _, in CallSite _) => throw new InvalidOperationException(StubMessage);

    private CaseDeclarations(ImmutableArray<OverloadBinding> overloads, ImmutableArray<AbstractTypeDeclaration> abstractTypes)
        : base(SetName, 0, SourceText, overloads, abstractTypes)
    {
    }

    /// <summary>
    /// The set that the declarations of a case make, wrapped to be added to a builder; <see langword="null"/> when the case declares no function.
    /// </summary>
    /// <param name="declarations">The declarations of the case: functions and identifiers.</param>
    /// <exception cref="NotImplementedException">A type of a declaration is not one that the library supports.</exception>
    internal static CelExtension? Create(IReadOnlyList<Decl> declarations)
    {
        ImmutableArray<OverloadBinding>.Builder overloads = ImmutableArray.CreateBuilder<OverloadBinding>();
        Dictionary<string, CelType> abstractTypes = new(StringComparer.Ordinal);
        List<string> order = [];
        foreach (Decl declaration in declarations)
        {
            if (declaration.DeclKindCase == Decl.DeclKindOneofCase.Function)
            {
                foreach (Decl.Types.FunctionDecl.Types.Overload overload in declaration.Function.Overloads)
                {
                    overloads.Add(Binding(declaration.Name, overload, abstractTypes, order));
                }
            }
            else if (declaration.DeclKindCase == Decl.DeclKindOneofCase.Ident)
            {
                Collect(declaration.Ident.Type, abstractTypes, order);
            }
        }

        if (overloads.Count == 0)
        {
            return null;
        }

        ImmutableArray<AbstractTypeDeclaration>.Builder types = ImmutableArray.CreateBuilder<AbstractTypeDeclaration>(order.Count);
        foreach (string name in order)
        {
            types.Add(new AbstractTypeDeclaration(name, abstractTypes[name]));
        }

        return new CelExtension(SetName, 0, new CaseDeclarations(overloads.ToImmutable(), types.MoveToImmutable()), [0]);
    }

    // The overload as the library declares it: the identifier, the call style, the parameter types and the result type.
    private static OverloadBinding Binding(string function, Decl.Types.FunctionDecl.Types.Overload overload, Dictionary<string, CelType> abstractTypes, List<string> order)
    {
        ImmutableArray<CelType>.Builder parameters = ImmutableArray.CreateBuilder<CelType>(overload.Params.Count);
        foreach (Type parameter in overload.Params)
        {
            Collect(parameter, abstractTypes, order);
            parameters.Add(TypeConversion.ToCelType(parameter));
        }

        Collect(overload.ResultType, abstractTypes, order);
        var declaration = new CelOverloadDecl(overload.OverloadId, overload.IsInstanceFunction, parameters.MoveToImmutable(), TypeConversion.ToCelType(overload.ResultType));
        return new OverloadBinding(function, declaration, Stub, 0, null, SourceText, isApplication: true);
    }

    // The abstract types that a type names, at any depth, each by the first form in which a case writes it; optional_type is declared by
    // the set optional.
    private static void Collect(Type type, Dictionary<string, CelType> abstractTypes, List<string> order)
    {
        switch (type.TypeKindCase)
        {
            case Type.TypeKindOneofCase.AbstractType:
                string name = type.AbstractType.Name;
                if (!string.Equals(name, TypeConversion.OptionalTypeName, StringComparison.Ordinal) && !abstractTypes.ContainsKey(name))
                {
                    abstractTypes.Add(name, TypeConversion.ToCelType(type));
                    order.Add(name);
                }

                foreach (Type parameter in type.AbstractType.ParameterTypes)
                {
                    Collect(parameter, abstractTypes, order);
                }

                break;
            case Type.TypeKindOneofCase.ListType:
                Collect(type.ListType.ElemType, abstractTypes, order);
                break;
            case Type.TypeKindOneofCase.MapType:
                Collect(type.MapType.KeyType, abstractTypes, order);
                Collect(type.MapType.ValueType, abstractTypes, order);
                break;
            case Type.TypeKindOneofCase.Type_:
                Collect(type.Type_, abstractTypes, order);
                break;
            default:
                break;
        }
    }
}
