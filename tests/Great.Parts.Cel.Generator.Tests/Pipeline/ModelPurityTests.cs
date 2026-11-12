using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Pipeline;

// The models of the pipeline are values: a model that held a symbol, a syntax node, the compilation or a location would keep the
// compilation alive, make equal models unequal and defeat the cache of the steps. The test walks the types of the models by reflection
// (a test project may use it; the generator itself does not) and fails on a field of such a type, at any depth.
public sealed class ModelPurityTests
{
    private static readonly Assembly GeneratorAssembly = typeof(CelTypeContextGenerator).Assembly;

    // The values that the steps pass on: the model of a context, the facts of the compilation, the diagnostics with their places.
    private static readonly string[] Roots = ["ContextModel", "CelTypeModel", "CompilationFacts", "DiagnosticModel", "LocationInfo", "Shape"];

    [Fact]
    [Trait("Requirement", "P-2")]
    public void Models_HoldNoSymbolNoSyntaxNodeNoCompilationAndNoLocation()
    {
        var seen = new HashSet<Type>();
        var violations = new List<string>();
        foreach (string name in Roots)
        {
            Type root = Assert.Single(GeneratorAssembly.GetTypes(), type => type.Name == name && type.Namespace == "Great.Parts.Cel.Generator");
            Walk(root, root.Name, seen, violations);
        }

        Assert.Empty(violations);
    }

    // The walk reaches the closed types of the model: the record types of the shapes are read from the subclasses of Shape too.
    [Fact]
    [Trait("Requirement", "P-2")]
    public void Shapes_AreRecordsOfTheModelWithTheirSubclassesWalked()
    {
        Type shape = Assert.Single(GeneratorAssembly.GetTypes(), type => type.Name == "Shape" && type.Namespace == "Great.Parts.Cel.Generator");
        Type[] shapes = [.. GeneratorAssembly.GetTypes().Where(type => shape.IsAssignableFrom(type) && type != shape)];

        Assert.NotEmpty(shapes);
        var violations = new List<string>();
        foreach (Type type in shapes)
        {
            Walk(type, type.Name, [], violations);
        }

        Assert.Empty(violations);
    }

    private static void Walk(Type type, string path, HashSet<Type> seen, List<string> violations)
    {
        if (!seen.Add(type))
        {
            return;
        }

        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (Type part in Parts(field.FieldType))
            {
                if (IsForbidden(part))
                {
                    violations.Add(path + "." + field.Name + " : " + part.FullName);
                }
                else if (part.Assembly == GeneratorAssembly)
                {
                    Walk(part, path + "." + field.Name, seen, violations);
                }
            }
        }
    }

    // The type and the types it is made of: the element of an array, the arguments of a generic type, at any depth.
    private static IEnumerable<Type> Parts(Type type)
    {
        yield return type;
        if (type.HasElementType)
        {
            foreach (Type element in Parts(type.GetElementType()!))
            {
                yield return element;
            }
        }

        if (type.IsGenericType)
        {
            foreach (Type argument in type.GetGenericArguments().SelectMany(Parts))
            {
                yield return argument;
            }
        }
    }

    private static bool IsForbidden(Type type) =>
        typeof(ISymbol).IsAssignableFrom(type)
        || typeof(SyntaxNode).IsAssignableFrom(type)
        || typeof(SyntaxTree).IsAssignableFrom(type)
        || typeof(Compilation).IsAssignableFrom(type)
        || typeof(SemanticModel).IsAssignableFrom(type)
        || typeof(Location).IsAssignableFrom(type);
}
