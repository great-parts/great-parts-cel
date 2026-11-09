using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Properties.Generators;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Configuration;

// An environment from a description in JSON equals the environment of the equal builder calls (docs/design/tests.md,
// "Environment from JSON"): Build gives both an environment or both the same errors, and in both environments the same texts
// give the same compile results, values and evaluation errors.
public sealed partial class EnvironmentJsonTests
{
    private const ulong Seed = 20261111;
    private const int Cases = 400;

    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "+R-8")]
    public void JsonEqualsBuilder() =>
        PropertyLoop.Check(nameof(JsonEqualsBuilder), DescriptionModel.Generator, Seed, Cases, Violation, model => model.Json);

    // The first difference between the two environments of a description, or null when there is none.
    private static string? Violation(DescriptionModel model)
    {
        CelEnvironmentBuilder fromJson = NewBuilder(model);
        ImmutableArray<CelError> applied = CelEnvironmentJson.Apply(fromJson, Encoding.UTF8.GetBytes(model.Json));
        if (!applied.IsEmpty)
        {
            return "Apply failed: " + Errors(applied);
        }

        AddImplementations(fromJson, model);
        CelEnvironmentBuilder fromCalls = NewBuilder(model);
        AddCalls(fromCalls, model);
        AddImplementations(fromCalls, model);

        CelBuildResult left = fromJson.Build();
        CelBuildResult right = fromCalls.Build();
        if (left.IsSuccess != right.IsSuccess)
        {
            return $"Build: JSON {Outcome(left)}, calls {Outcome(right)}";
        }

        if (!left.IsSuccess)
        {
            return BuildErrorsDiffer(left.Errors, right.Errors);
        }

        return CompareEnvironments(left.GetEnvironmentOrThrow(), right.GetEnvironmentOrThrow(), model);
    }

    private static string Outcome(CelBuildResult result) => result.IsSuccess ? "succeeds" : "fails with " + Errors(result.Errors);

    private static string Errors(IEnumerable<CelError> errors)
    {
        var text = new StringBuilder();
        foreach (CelError error in errors)
        {
            text.Append(CultureInfo.InvariantCulture, $"[{error.Code} {error.Message}]");
        }

        return text.ToString();
    }

    // The calls in code that both builders get first.
    private static CelEnvironmentBuilder NewBuilder(DescriptionModel model)
    {
        var builder = new CelEnvironmentBuilder(CelStandardLibrary.V1);
        if (model.CodeContainer is not null)
        {
            builder.UseContainer(model.CodeContainer);
        }

        if (model.CodeVariable is not null)
        {
            builder.AddVariable(model.CodeVariable.Name, model.CodeVariable.Type);
        }

        if (model.CodeType is not null)
        {
            builder.AddTypes(CelTypeProvider.Create("code", CelObjectTypeBuilder.ForDictionary(model.CodeType).Build()));
        }

        return builder;
    }

    // The calls that equal the description: the container, the variables, the functions, one provider of the types.
    private static void AddCalls(CelEnvironmentBuilder builder, DescriptionModel model)
    {
        if (model.Container.Length > 0)
        {
            builder.UseContainer(model.Container);
        }

        foreach (VariableModel variable in model.Variables)
        {
            builder.AddVariable(variable.Name, variable.Type);
        }

        foreach (FunctionModel function in model.Functions)
        {
            var overloads = new List<CelOverloadDecl>();
            foreach (OverloadModel overload in function.Overloads)
            {
                overloads.Add(overload.Target is { } target
                    ? CelOverloadDecl.Receiver(overload.Id, target, overload.Args, overload.Return)
                    : CelOverloadDecl.Global(overload.Id, overload.Args, overload.Return));
            }

            builder.AddFunction(new CelFunctionDecl(function.Name, [.. overloads]));
        }

        if (model.Types.Count > 0)
        {
            var types = new List<CelObjectType>();
            foreach (ObjectTypeModel type in model.Types)
            {
                CelObjectTypeBuilder typeBuilder = CelObjectTypeBuilder.ForDictionary(type.Name);
                foreach (FieldModel field in type.Fields)
                {
                    typeBuilder.AddField(field.Name, field.Type);
                }

                types.Add(typeBuilder.Build());
            }

            builder.AddTypes(CelTypeProvider.Create("model", [.. types]));
        }
    }

    // The same implementation of every overload in both builders: a fixed value that fits the result type, with a type
    // parameter as dyn — null for an object type and a wrapper.
    private static void AddImplementations(CelEnvironmentBuilder builder, DescriptionModel model)
    {
        foreach (FunctionModel function in model.Functions)
        {
            foreach (OverloadModel overload in function.Overloads)
            {
                CelValue result = Fixed(overload.Return);
                builder.AddFunctionImplementation(overload.Id, _ => result);
            }
        }
    }

    private static CelValue Fixed(CelType type) => Value(type, full: false);

    // A value of the type; a collection has one element when full, a wrapper holds its value when full.
    private static CelValue Value(CelType type, bool full) => type.Kind switch
    {
        CelTypeKind.UInt => CelValue.FromUInt64(7),
        CelTypeKind.Double => CelValue.FromDouble(1.5),
        CelTypeKind.Bool => CelValue.FromBoolean(true),
        CelTypeKind.String => CelValue.FromString("v"),
        CelTypeKind.Bytes => CelValue.FromBytes(new byte[] { 1, 2 }),
        CelTypeKind.List => CelValue.FromList(full ? CelList.Create(Value(type.Parameters[0], full: false)) : CelList.Create()),
        CelTypeKind.Map => CelValue.FromMap(CelMap.Create(full ? [new(Key(type.Parameters[0]), Value(type.Parameters[1], full: false))] : [])),
        CelTypeKind.Type => CelValue.FromType(CelType.Int),
        CelTypeKind.Wrapper => full ? Value(type.Parameters[0], full: false) : CelValue.Null,
        CelTypeKind.Null or CelTypeKind.Object => CelValue.Null,
        _ => CelValue.FromInt64(7),
    };

    private static CelValue Key(CelType type) => type.Kind switch
    {
        CelTypeKind.UInt => CelValue.FromUInt64(1),
        CelTypeKind.Bool => CelValue.FromBoolean(true),
        CelTypeKind.Int => CelValue.FromInt64(1),
        _ => CelValue.FromString("k"),
    };

    // Both fail with the same codes in the same order, and with the same messages once the names of sources are taken out. Two
    // messages that differ only by the source are one when both are from the application, so the messages compare as sets.
    private static string? BuildErrorsDiffer(ImmutableArray<CelError> left, ImmutableArray<CelError> right)
    {
        List<string> shownLeft = Keys(left);
        List<string> shownRight = Keys(right);
        return shownLeft.SequenceEqual(shownRight) ? null : $"Build errors: JSON {Errors(left)}, calls {Errors(right)}";
    }

    // The sorted distinct texts of code and normalized message; the order of the codes is the order of the sort.
    private static List<string> Keys(ImmutableArray<CelError> errors)
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (CelError error in errors)
        {
            keys.Add(string.Create(CultureInfo.InvariantCulture, $"{(int)error.Code:D5} {error.Code} {Normalized(error.Message)}"));
        }

        return [.. keys];
    }

    // The message without the names of sources; the two containers of a conflict as a sorted pair, since the source decides which
    // is named first.
    private static string Normalized(string message)
    {
        string text = SourceName().Replace(message, "<source>");
        return ContainerPair().Replace(text, match =>
        {
            string first = match.Groups[1].Value;
            string second = match.Groups[2].Value;
            return string.CompareOrdinal(first, second) <= 0 ? $"containers '{first}' and '{second}'" : $"containers '{second}' and '{first}'";
        });
    }

    [GeneratedRegex(@"type source '[^']*'|environment JSON( '[^']*')?|application")]
    private static partial Regex SourceName();

    [GeneratedRegex(@"container '([^']*)' of <source> conflicts with container '([^']*)' of <source>")]
    private static partial Regex ContainerPair();
}
