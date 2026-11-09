using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using CsCheck;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Properties.Evaluation;
using Great.Parts.Cel.Properties.Generators;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Properties.Configuration;

// The comparison of the two environments of a description: the same texts, compiled and evaluated in both.
public sealed partial class EnvironmentJsonTests
{
    // The first difference over the texts of the description, with the text, or null.
    private static string? CompareEnvironments(CelEnvironment left, CelEnvironment right, DescriptionModel model)
    {
        CelBindings bindings = Bindings(model);
        foreach (string text in Texts(model))
        {
            CelCompileResult<CelValue> compiledLeft = left.Compile<CelValue>(text);
            CelCompileResult<CelValue> compiledRight = right.Compile<CelValue>(text);
            string? difference = CompareCompiled(compiledLeft, compiledRight)
                ?? (compiledLeft.IsSuccess ? CompareEvaluated(compiledLeft.GetProgramOrThrow(), compiledRight.GetProgramOrThrow(), bindings) : null);
            if (difference is not null)
            {
                return $"'{text}': {difference}";
            }
        }

        return null;
    }

    // Compile results by the result type, and by the code, stage, message and location of each error.
    private static string? CompareCompiled(CelCompileResult<CelValue> left, CelCompileResult<CelValue> right)
    {
        if (left.IsSuccess != right.IsSuccess)
        {
            return $"compile: JSON {CompiledOutcome(left)}, calls {CompiledOutcome(right)}";
        }

        return CompiledOutcome(left) == CompiledOutcome(right) ? null : $"compile: JSON {CompiledOutcome(left)}, calls {CompiledOutcome(right)}";
    }

    private static string CompiledOutcome(CelCompileResult<CelValue> result) =>
        result.IsSuccess ? "program of " + result.GetProgramOrThrow().ResultType : ErrorKeys(result.Errors);

    private static string? CompareEvaluated(CelProgram<CelValue> left, CelProgram<CelValue> right, CelBindings bindings)
    {
        CelEvalResult<CelValue> evaluatedLeft = left.Evaluate(bindings);
        CelEvalResult<CelValue> evaluatedRight = right.Evaluate(bindings);
        if (evaluatedLeft.IsSuccess && evaluatedRight.IsSuccess)
        {
            return StructuralEquality.Equal(evaluatedLeft.Value, evaluatedRight.Value)
                ? null
                : $"values: JSON {Shown(evaluatedLeft.Value)}, calls {Shown(evaluatedRight.Value)}";
        }

        if (!evaluatedLeft.IsSuccess && !evaluatedRight.IsSuccess)
        {
            return ErrorKeys(evaluatedLeft.Errors) == ErrorKeys(evaluatedRight.Errors)
                ? null
                : $"evaluation errors: JSON {ErrorKeys(evaluatedLeft.Errors)}, calls {ErrorKeys(evaluatedRight.Errors)}";
        }

        return $"evaluation: JSON {EvaluationOutcome(evaluatedLeft)}, calls {EvaluationOutcome(evaluatedRight)}";
    }

    private static string EvaluationOutcome(CelEvalResult<CelValue> result) =>
        result.IsSuccess ? "value " + Shown(result.Value) : ErrorKeys(result.Errors);

    private static string Shown(CelValue value)
    {
        var text = new StringBuilder();
        TypedExpressions.Print(text, value);
        return text.ToString();
    }

    // The code, stage, message and location of each error.
    private static string ErrorKeys(ImmutableArray<CelError> errors)
    {
        var text = new StringBuilder();
        foreach (CelError error in errors)
        {
            text.Append(CultureInfo.InvariantCulture, $"[{error.Code} {error.Stage} {error.Location.Offset}:{error.Location.Line}:{error.Location.Column} {error.Message}]");
        }

        return text.ToString();
    }

    // The texts of a description: each variable by its forms through the container, and its type(); each field of an object
    // variable with a dot and with has(); each overload called with variables or literals of its parameter types; a name that no
    // declaration has.
    private static List<string> Texts(DescriptionModel model)
    {
        var texts = new List<string> { "undeclared_name" };
        foreach (VariableModel variable in Variables(model))
        {
            foreach (string form in Forms(variable.Name, model.EffectiveContainer))
            {
                texts.Add(form);
            }

            texts.Add($"type({variable.Name})");
            if (ObjectTypeOf(model, variable.Type) is { } type)
            {
                foreach (FieldModel field in type.Fields)
                {
                    texts.Add($"{variable.Name}.{field.Name}");
                    texts.Add($"has({variable.Name}.{field.Name})");
                }
            }
        }

        foreach (FunctionModel function in model.Functions)
        {
            foreach (OverloadModel overload in function.Overloads)
            {
                AddCalls(texts, model, function, overload);
            }
        }

        return texts;
    }

    // A name as written, with a leading dot, and without the container that it starts with.
    private static List<string> Forms(string name, string container)
    {
        List<string> forms = [name, "." + name];
        if (container.Length > 0 && name.StartsWith(container + ".", StringComparison.Ordinal))
        {
            forms.Add(name[(container.Length + 1)..]);
        }

        return forms;
    }

    // A global overload by each form of the name of its function; a receiver overload with the first argument as the receiver.
    private static void AddCalls(List<string> texts, DescriptionModel model, FunctionModel function, OverloadModel overload)
    {
        var arguments = new List<string>();
        foreach (CelType argument in overload.Args)
        {
            arguments.Add(Argument(model, argument));
        }

        string list = string.Join(", ", arguments);
        if (overload.Target is { } target)
        {
            texts.Add($"({Argument(model, target)}).{function.Name}({list})");
            return;
        }

        foreach (string form in Forms(function.Name, model.EffectiveContainer))
        {
            texts.Add($"{form}({list})");
        }
    }

    // A literal of the type; for an object type a variable of that type, or null.
    private static string Argument(DescriptionModel model, CelType type)
    {
        switch (type.Kind)
        {
            case CelTypeKind.UInt:
                return "1u";
            case CelTypeKind.Double:
                return "1.5";
            case CelTypeKind.Bool:
                return "true";
            case CelTypeKind.String:
                return "'s'";
            case CelTypeKind.Bytes:
                return "b'x'";
            case CelTypeKind.List:
                return "[]";
            case CelTypeKind.Map:
                return "{}";
            case CelTypeKind.Type:
                return "int";
            case CelTypeKind.Null or CelTypeKind.Wrapper:
                return "null";
            case CelTypeKind.Object:
                foreach (VariableModel variable in Variables(model))
                {
                    if (variable.Type.Equals(type))
                    {
                        return variable.Name;
                    }
                }

                return "null";
            default:
                return "1";
        }
    }

    private static List<VariableModel> Variables(DescriptionModel model)
    {
        List<VariableModel> variables = [.. model.Variables];
        if (model.CodeVariable is not null)
        {
            variables.Add(model.CodeVariable);
        }

        return variables;
    }

    // The first description of an object type of the model, for the type of a variable.
    private static ObjectTypeModel? ObjectTypeOf(DescriptionModel model, CelType type)
    {
        if (type.Kind != CelTypeKind.Object)
        {
            return null;
        }

        foreach (ObjectTypeModel candidate in model.Types)
        {
            if (string.Equals(candidate.Name, type.Name, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    // A value of each declared type, dictionaries for the object types, one variable in five unbound.
    private static CelBindings Bindings(DescriptionModel model)
    {
        var random = new PCG(0, model.BindingSeed);
        var bindings = new CelBindings();
        var bound = new HashSet<string>(StringComparer.Ordinal);
        foreach (VariableModel variable in Variables(model))
        {
            if (!bound.Add(variable.Name) || random.Next(5) == 0)
            {
                continue;
            }

            if (variable.Type.Kind != CelTypeKind.Object)
            {
                bindings.Add(variable.Name, Value(variable.Type, full: true));
            }
            else if (ObjectTypeOf(model, variable.Type) is { } type)
            {
                bindings.Add<object>(variable.Name, ObjectDictionary(model, type, random, 0));
            }
        }

        return bindings;
    }

    // The dictionary of an object type: a key for most fields, the value by the type of the field, a nested dictionary for an
    // object type two levels deep at most.
    private static Dictionary<string, object?> ObjectDictionary(DescriptionModel model, ObjectTypeModel type, PCG random, int depth)
    {
        var entries = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (FieldModel field in type.Fields)
        {
            if (random.Next(5) == 0)
            {
                continue;
            }

            if (field.Type.Kind != CelTypeKind.Object)
            {
                entries[field.Name] = Value(field.Type, full: true);
            }
            else if (depth < 2 && ObjectTypeOf(model, field.Type) is { } nested)
            {
                entries[field.Name] = ObjectDictionary(model, nested, random, depth + 1);
            }
        }

        return entries;
    }
}
