using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

// A member that the type reads as a field: its number among the fields, its symbol and the shape of its read.
internal sealed record LiteralMember(int Number, ISymbol Symbol, Shape Shape);

// The plan of the object literals of a type that [CelObject(…, AllowObjectLiterals = true)] lists ("Object literals" of the page of the
// created code): the constructor, which fields are parameters, which members are set how, and which fields every literal must set.
// A type that the created code cannot create is GPCEL012 at the argument of the attribute, with the reason, and gets no literals.
internal static class LiteralReader
{
    internal static LiteralModel? Read(INamedTypeSymbol type, RootInfo root, List<LiteralMember> members, DiagnosticSink sink)
    {
        LiteralModel? model = TryRead(type, members, out string reason);
        if (model is null)
        {
            sink.Report(DiagnosticIds.ObjectLiteralType, root.LiteralPlace, TypeNames.Display(type), reason);
        }

        return model;
    }

    private static LiteralModel? TryRead(INamedTypeSymbol type, List<LiteralMember> members, out string reason)
    {
        reason = string.Empty;
        if (type.TypeKind == TypeKind.Interface)
        {
            reason = Reasons.LiteralInterface;
            return null;
        }

        if (type.IsAbstract)
        {
            reason = Reasons.LiteralAbstract;
            return null;
        }

        IParameterSymbol[]? parameters = ChooseConstructor(type, out reason);
        if (parameters is null)
        {
            return null;
        }

        var fields = new LiteralFieldModel?[members.Count];
        for (int index = 0; index < parameters.Length; index++)
        {
            if (!TryParameterField(parameters[index], index, members, fields, out reason))
            {
                return null;
            }
        }

        foreach (LiteralMember member in members)
        {
            fields[member.Number] ??= MemberField(member);
            if (fields[member.Number]!.Required && !fields[member.Number]!.Settable)
            {
                reason = Reasons.LiteralRequiredMember(member.Symbol.Name);
                return null;
            }
        }

        string? hidden = RequiredMemberOutsideFields(type, members);
        if (hidden is not null)
        {
            reason = Reasons.LiteralRequiredMember(hidden);
            return null;
        }

        var all = new List<LiteralFieldModel>(fields.Length);
        foreach (LiteralFieldModel? field in fields)
        {
            all.Add(field!);
        }

        return new LiteralModel(parameters.Length, type.TypeKind == TypeKind.Struct, EquatableArray.From(all));
    }

    // The parameters of the constructor that the created code calls: a public one without parameters (a structure always has one),
    // otherwise the only public one. Null with the reason when there is none or it is not the only one.
    private static IParameterSymbol[]? ChooseConstructor(INamedTypeSymbol type, out string reason)
    {
        reason = string.Empty;
        bool parameterless = type.TypeKind == TypeKind.Struct;
        var withParameters = new List<IMethodSymbol>();
        foreach (IMethodSymbol constructor in type.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            if (constructor.Parameters.Length == 0)
            {
                parameterless = true;
            }
            else
            {
                withParameters.Add(constructor);
            }
        }

        if (parameterless)
        {
            return [];
        }

        if (withParameters.Count == 0)
        {
            reason = Reasons.LiteralNoConstructor;
            return null;
        }

        if (withParameters.Count > 1)
        {
            reason = Reasons.LiteralSeveralConstructors;
            return null;
        }

        return [.. withParameters[0].Parameters];
    }

    // A parameter is the field whose member has its name, ignoring case, and its type; without a default it is a required field.
    private static bool TryParameterField(IParameterSymbol parameter, int index, List<LiteralMember> members, LiteralFieldModel?[] fields, out string reason)
    {
        reason = string.Empty;
        LiteralMember? match = Match(parameter.Name, members);
        if (match is null || parameter.RefKind != RefKind.None || fields[match.Number] is not null)
        {
            reason = Reasons.LiteralParameterWithoutField(parameter.Name);
            return false;
        }

        ITypeSymbol? memberType = MemberCatalog.TypeOf(match.Symbol);
        if (memberType is null || !SymbolEqualityComparer.Default.Equals(parameter.Type, memberType))
        {
            reason = Reasons.LiteralParameterType(parameter.Name);
            return false;
        }

        CreateShape? shape = CreateShapes.Of(parameter.Type, match.Shape, false);
        bool hasDefault = parameter.HasExplicitDefaultValue;
        if (shape is null && !hasDefault)
        {
            reason = Reasons.LiteralRequiredMember(parameter.Name);
            return false;
        }

        fields[match.Number] = new LiteralFieldModel(
            match.Number,
            shape is not null,
            !hasDefault,
            shape,
            index,
            hasDefault ? DefaultValues.Text(parameter) : string.Empty,
            SetterKind.Assign,
            string.Empty,
            string.Empty,
            string.Empty);
        return true;
    }

    private static LiteralMember? Match(string name, List<LiteralMember> members)
    {
        LiteralMember? ignoringCase = null;
        foreach (LiteralMember member in members)
        {
            if (string.Equals(member.Symbol.Name, name, StringComparison.Ordinal))
            {
                return member;
            }

            if (ignoringCase is null && string.Equals(member.Symbol.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                ignoringCase = member;
            }
        }

        return ignoringCase;
    }

    // A member that is not a parameter: settable when it has a public set or init accessor or is a mutable field, and its type has a
    // creation row. A required member goes in the object initializer, an init member that is not required through its accessor.
    private static LiteralFieldModel MemberField(LiteralMember member)
    {
        ISymbol symbol = member.Symbol;
        bool required = IsRequired(symbol);
        ITypeSymbol? type = MemberCatalog.TypeOf(symbol);
        CreateShape? shape = type is null ? null : CreateShapes.Of(type, member.Shape, false);
        SetterKind? setter = SetterOf(symbol, required);
        if (shape is null || setter is null)
        {
            return new LiteralFieldModel(member.Number, false, required, null, -1, string.Empty, SetterKind.Assign, string.Empty, string.Empty, string.Empty);
        }

        return new LiteralFieldModel(
            member.Number,
            true,
            required,
            shape,
            -1,
            string.Empty,
            setter.Value,
            Literals.Identifier(symbol.Name),
            TypeNames.ClrName(symbol.ContainingType, false),
            symbol.Name);
    }

    private static SetterKind? SetterOf(ISymbol symbol, bool required)
    {
        switch (symbol)
        {
            case IPropertySymbol { IsStatic: false, SetMethod: { DeclaredAccessibility: Accessibility.Public } set }:
                return required ? SetterKind.Initializer : set.IsInitOnly ? SetterKind.Accessor : SetterKind.Assign;
            case IFieldSymbol { IsStatic: false, IsReadOnly: false, IsConst: false }:
                return required ? SetterKind.Initializer : SetterKind.Assign;
            default:
                return null;
        }
    }

    private static bool IsRequired(ISymbol symbol) => symbol switch
    {
        IPropertySymbol property => property.IsRequired,
        IFieldSymbol field => field.IsRequired,
        _ => false,
    };

    // A required member of the type that is not a field (hidden, ignored or left out): C# demands it in every object initializer, so
    // the created code could not compile. Its name, or null.
    private static string? RequiredMemberOutsideFields(INamedTypeSymbol type, List<LiteralMember> members)
    {
        for (INamedTypeSymbol? current = type; current is not null && current.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType); current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers())
            {
                if (!member.IsStatic && IsRequired(member) && !IsField(member.Name, members))
                {
                    return member.Name;
                }
            }
        }

        return null;
    }

    private static bool IsField(string name, List<LiteralMember> members)
    {
        foreach (LiteralMember member in members)
        {
            if (string.Equals(member.Symbol.Name, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

// The C# text of the default value of a parameter, which the created code passes when the literal does not set the field.
internal static class DefaultValues
{
    internal static string Text(IParameterSymbol parameter)
    {
        object? value = parameter.ExplicitDefaultValue;
        if (value is null)
        {
            return "default!";
        }

        if (value is string text)
        {
            return Literals.Quote(text);
        }

        if (value is bool flag)
        {
            return flag ? "true" : "false";
        }

        string type = TypeNames.ClrName(parameter.Type, true);
        return value is char character
            ? "(char)" + ((int)character).ToString(CultureInfo.InvariantCulture)
            : "(" + type + ")(" + Number(value) + ")";
    }

    private static string Number(object value) => value switch
    {
        uint unsigned => unsigned.ToString(CultureInfo.InvariantCulture) + "U",
        long wide => wide.ToString(CultureInfo.InvariantCulture) + "L",
        ulong unsignedWide => unsignedWide.ToString(CultureInfo.InvariantCulture) + "UL",
        float single => Floating(single, single.ToString("R", CultureInfo.InvariantCulture), "float", "F"),
        double number => Floating(number, number.ToString("R", CultureInfo.InvariantCulture), "double", "D"),
        decimal money => money.ToString(CultureInfo.InvariantCulture) + "M",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
    };

    private static string Floating(double value, string text, string type, string suffix)
    {
        if (double.IsNaN(value))
        {
            return type + ".NaN";
        }

        if (double.IsInfinity(value))
        {
            return (value < 0 ? "-" : string.Empty) + type + ".PositiveInfinity";
        }

        return text + suffix;
    }
}
