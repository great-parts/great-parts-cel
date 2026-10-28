using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>Questions to a syntax tree that the checks of C# sources ask. They do not need a compilation.</summary>
internal static class SyntaxQueries
{
    private const string AttributeSuffix = "Attribute";

    /// <summary>
    /// Returns every active preprocessor directive of the tree. Directives are trivia of tokens, and a token that the parser
    /// skips as unknown syntax keeps its trivia, so the search goes into the trivia that holds skipped tokens too. Roslyn also
    /// parses directives in a branch that the symbols do not activate; those are not returned.
    /// </summary>
    /// <param name="root">The root of the tree.</param>
    internal static IEnumerable<DirectiveTriviaSyntax> Directives(SyntaxNode root) =>
        root.DescendantTrivia(descendIntoTrivia: true)
            .Where(trivia => trivia.IsDirective)
            .Select(trivia => trivia.GetStructure())
            .OfType<DirectiveTriviaSyntax>()
            .Where(directive => directive.IsActive);

    /// <summary>
    /// Returns every attribute whose name, with any qualifier, is <paramref name="name"/> or <paramref name="name"/> followed by
    /// <c>Attribute</c>, the way the attribute can be written in source.
    /// </summary>
    /// <param name="root">The root of the tree or the node to search.</param>
    /// <param name="name">The simple name without the suffix, such as <c>GeneratedCode</c>.</param>
    internal static IEnumerable<AttributeSyntax> Attributes(SyntaxNode root, string name) =>
        root.DescendantNodes().OfType<AttributeSyntax>().Where(attribute => IsNamed(attribute, name));

    private static bool IsNamed(AttributeSyntax attribute, string name)
    {
        string? simple = attribute.Name switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
            SimpleNameSyntax simpleName => simpleName.Identifier.ValueText,
            _ => null,
        };
        return string.Equals(simple, name, StringComparison.Ordinal) || string.Equals(simple, name + AttributeSuffix, StringComparison.Ordinal);
    }

    /// <summary>Returns true when the tree has an <c>#if</c> or <c>#elif</c> directive, so another symbol set can parse it differently.</summary>
    /// <param name="root">The root of the tree.</param>
    internal static bool HasConditionalDirective(SyntaxNode root) =>
        Directives(root).Any(directive => directive.IsKind(SyntaxKind.IfDirectiveTrivia) || directive.IsKind(SyntaxKind.ElifDirectiveTrivia));
}
