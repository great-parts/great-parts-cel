using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Checking;

// Gradual type checking of a parsed expression against the declarations of an environment. The tree is
// visited depth first: the children of a node from left to right, then the node. The type of a node is
// a CelType, or null for the type error: a node with an error is assignable to every type and produces
// no further errors, and checking goes on, so every check error of the text is returned in the order
// of the traversal.
internal sealed class Checker
{
    private readonly ParsedExpression _parsed;
    private readonly DeclarationIndex _declarations;
    private readonly Dictionary<int, Reference> _references = [];
    private readonly Dictionary<int, CelType> _types = [];
    private readonly ImmutableArray<CelError>.Builder _errors = ImmutableArray.CreateBuilder<CelError>();

    private Checker(ParsedExpression parsed, DeclarationIndex declarations)
    {
        _parsed = parsed;
        _declarations = declarations;
    }

    internal static CheckResult Check(ParsedExpression parsed, DeclarationIndex declarations)
    {
        var checker = new Checker(parsed, declarations);
        checker.Visit(parsed.Root);
        return checker._errors.Count == 0
            ? new CheckResult(new CheckedExpression(parsed, checker._references, checker._types))
            : new CheckResult(checker._errors.ToImmutable());
    }

    private CelType? Visit(ExprNode node)
    {
        CelType? type = node switch
        {
            ConstantNode constant => ConstantType(constant.Value.Kind),
            IdentNode identifier => VisitIdentifier(identifier),
            CallNode call => VisitCall(call),
            ListNode list => VisitList(list),
            StructNode map => VisitMap(map),
            _ => throw new NotImplementedException(),
        };

        if (type is not null)
        {
            _types.Add(node.Id, type);
        }

        return type;
    }

    private static CelType ConstantType(CelValueKind kind) => kind switch
    {
        CelValueKind.Null => CelType.Null,
        CelValueKind.Bool => CelType.Bool,
        CelValueKind.Int => CelType.Int,
        CelValueKind.UInt => CelType.UInt,
        CelValueKind.Double => CelType.Double,
        CelValueKind.String => CelType.String,
        CelValueKind.Bytes => CelType.Bytes,
        _ => throw new NotImplementedException(),
    };

    private CelType? VisitIdentifier(IdentNode identifier)
    {
        if (!_declarations.TryGetVariable(identifier.Name, out CelVariableDecl? variable))
        {
            _errors.Add(CheckErrors.UndeclaredReference(identifier.Name, Location(identifier)));
            return null;
        }

        _references.Add(identifier.Id, Reference.ToVariable(variable));
        return variable.Type;
    }

    private CelType? VisitCall(CallNode call)
    {
        ImmutableArray<ExprNode> arguments = call.Arguments;
        var argumentTypes = new CelType[arguments.Length];
        bool hasError = false;
        for (int i = 0; i < arguments.Length; i++)
        {
            CelType? argumentType = Visit(arguments[i]);
            hasError |= argumentType is null;
            argumentTypes[i] = argumentType ?? CelType.Dyn;
        }

        if (hasError)
        {
            return null;
        }

        if (!_declarations.IsFunction(call.Function))
        {
            _errors.Add(CheckErrors.UndeclaredReference(call.Function, Location(call)));
            return null;
        }

        ImmutableArray<OverloadBinding> candidates = Candidates(_declarations.GetOverloads(call.Function, arguments.Length), argumentTypes);
        if (candidates.IsEmpty)
        {
            _errors.Add(CheckErrors.NoMatchingOverload(call.Function, argumentTypes, Location(call)));
            return null;
        }

        _references.Add(call.Id, Reference.ToCandidates(candidates));
        return CommonType(candidates);
    }

    // The overloads whose parameters accept the argument types, in the order of the index.
    private static ImmutableArray<OverloadBinding> Candidates(ImmutableArray<OverloadBinding> overloads, CelType[] argumentTypes)
    {
        ImmutableArray<OverloadBinding>.Builder candidates = ImmutableArray.CreateBuilder<OverloadBinding>();
        foreach (OverloadBinding overload in overloads)
        {
            ImmutableArray<CelType> parameterTypes = overload.Declaration.ParameterTypes;
            bool accepts = true;
            for (int i = 0; i < argumentTypes.Length && accepts; i++)
            {
                accepts = AssignabilityRules.IsAssignable(argumentTypes[i], parameterTypes[i]);
            }

            if (accepts)
            {
                candidates.Add(overload);
            }
        }

        return candidates.ToImmutable();
    }

    // The result type of the candidates when they agree, otherwise dyn.
    private static CelType CommonType(ImmutableArray<OverloadBinding> candidates)
    {
        CelType type = candidates[0].Declaration.ResultType;
        for (int i = 1; i < candidates.Length; i++)
        {
            if (!candidates[i].Declaration.ResultType.Equals(type))
            {
                return CelType.Dyn;
            }
        }

        return type;
    }

    private CelType? VisitList(ListNode list)
    {
        var elements = new ElementType();
        foreach (ExprNode element in list.Elements)
        {
            elements.Add(Visit(element));
        }

        return elements.HasError ? null : CelType.ListOf(elements.Type);
    }

    // A map literal; message literals come with the complete grammar. The key types are not checked: a
    // key that cannot be a map key is an evaluation error.
    private CelType? VisitMap(StructNode map)
    {
        var keys = new ElementType();
        var values = new ElementType();
        foreach (StructEntry entry in map.Entries)
        {
            keys.Add(Visit(entry.Key));
            values.Add(Visit(entry.Value));
        }

        return keys.HasError || values.HasError ? null : CelType.MapOf(keys.Type, values.Type);
    }

    private CelSourceLocation Location(ExprNode node) => _parsed.Lines.GetLocation(_parsed.GetOffset(node.Id));

    // The type shared by the elements of a list, or by the keys or the values of a map: T when all have
    // the type T, dyn when there are none or their types differ.
    private struct ElementType
    {
        private CelType? _type;
        private bool _differ;

        internal bool HasError { get; private set; }

        internal readonly CelType Type => _differ || _type is null ? CelType.Dyn : _type;

        internal void Add(CelType? type)
        {
            if (type is null)
            {
                HasError = true;
            }
            else if (_type is null)
            {
                _type = type;
            }
            else if (!_type.Equals(type))
            {
                _differ = true;
            }
        }
    }
}
