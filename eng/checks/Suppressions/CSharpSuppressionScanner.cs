using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Reads the forms of the C# files: <c>#pragma warning disable</c>, <c>#nullable disable</c>, <c>[SuppressMessage]</c>,
/// <c>[UnconditionalSuppressMessage]</c> and <c>[GeneratedCode]</c> (<c>docs/design/eng-checks-source.md</c>, "suppressions").
/// The files are read as C# sources, so a form under <c>#if</c> is found once, and text in a string literal is not a form.
/// </summary>
internal sealed class CSharpSuppressionScanner : ISuppressionScanner
{
    private const string ReasonComment = "reason comment";
    private const string ReasonJustification = "reason in the Justification argument";
    private const string NullableKey = "nullable";
    private const string PragmaKey = "pragma";
    private const string GeneratedKey = "generated";
    private static readonly string[] SuppressMessageNames = ["SuppressMessage", "UnconditionalSuppressMessage"];

    /// <inheritdoc/>
    public IEnumerable<SuppressionForm> Scan(CheckContext context)
    {
        foreach (CSharpFile file in context.Sources.Files)
        {
            List<SourcePlace<Draft>> places =
            [
                .. file.Find(tree => Pragmas(tree, file.Text)),
                .. file.Find(Nullables),
                .. file.Find(SuppressMessages),
                .. file.Find(tree => GeneratedCodes(tree, file.Lines)),
            ];
            foreach (SourcePlace<Draft> place in places.OrderBy(place => place.Span.Start))
            {
                Draft draft = place.Value;
                yield return new SuppressionForm(place.Path, place.Position, draft.Description, draft.ReasonPlace, draft.Keys, draft.RuleIds, draft.Reason);
            }
        }
    }

    // ---- #pragma warning disable, #nullable disable ----

    private static IEnumerable<(TextSpan Span, Draft Value)> Pragmas(SyntaxTree tree, string text) => SyntaxQueries.Directives(tree.GetRoot())
        .OfType<PragmaWarningDirectiveTriviaSyntax>()
        .Where(directive => directive.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword) && !directive.DisableOrRestoreKeyword.IsMissing)
        .Select(directive =>
        {
            string[] ids = [.. directive.ErrorCodes
                .Select(code => SuppressionReason.NormalizeId(code.ToString().Trim()))
                .Where(id => id.Length > 0)];
            int end = directive.ErrorCodes.Count > 0 ? directive.ErrorCodes[^1].Span.End : directive.DisableOrRestoreKeyword.Span.End;
            string form = text[directive.SpanStart..end];

            // A directive without IDs turns off every warning: it counts under its own key, so no place is left out of the ratchet.
            string[] keys = ids.Length > 0 ? ids : [PragmaKey];
            return (directive.Span, new Draft($"'{form}'", ReasonComment, keys, ids, DirectiveComment(directive)));
        });

    private static IEnumerable<(TextSpan Span, Draft Value)> Nullables(SyntaxTree tree) => SyntaxQueries.Directives(tree.GetRoot())
        .OfType<NullableDirectiveTriviaSyntax>()
        .Where(directive => directive.SettingToken.IsKind(SyntaxKind.DisableKeyword) && !directive.SettingToken.IsMissing)
        .Select(directive =>
        {
            SyntaxToken last = directive.TargetToken.IsKind(SyntaxKind.None) ? directive.SettingToken : directive.TargetToken;
            string form = "#nullable " + directive.SettingToken.Text + (directive.TargetToken.IsKind(SyntaxKind.None) ? string.Empty : " " + directive.TargetToken.Text);
            return (TextSpan.FromBounds(directive.SpanStart, last.Span.End), new Draft($"'{form}'", ReasonComment, [NullableKey], [], DirectiveComment(directive)));
        });

    // The comment of a directive is trivia of its tokens: the trailing trivia of the last one, or the leading trivia of the token
    // that ends the directive.
    private static string DirectiveComment(DirectiveTriviaSyntax directive) => string.Join(
        ' ',
        directive.DescendantTokens().SelectMany(token => token.LeadingTrivia.Concat(token.TrailingTrivia)).Where(IsComment).Select(trivia => trivia.ToString()));

    // ---- [SuppressMessage], [UnconditionalSuppressMessage] ----

    private static IEnumerable<(TextSpan Span, Draft Value)> SuppressMessages(SyntaxTree tree) => SuppressMessageNames
        .SelectMany(name => SyntaxQueries.Attributes(tree.GetRoot(), name).Select(attribute => (Name: name, Attribute: attribute)))
        .Select(found =>
        {
            CheckIdKey check = KeyOfCheckId(found.Attribute);
            string[] keys = [check.Key];
            string[] ids = check.IsRuleId ? keys : [];
            string reason = TextOf(Argument(found.Attribute, "Justification", -1));
            string description = check.Written ? $"'[{found.Name}] {check.Key}'" : $"'[{found.Name}]'";
            return (FormSpan(found.Attribute), new Draft(description, ReasonJustification, keys, ids, reason));
        });

    // The key of the ratchet for the CheckId argument. A string literal, also in parentheses, gives its part before ':' as the
    // rule ID. Every other case counts under its own key and is not a rule ID: any other expression (a constant, a
    // concatenation, null, default) under its source text, a literal with nothing before ':' under its source text, an
    // attribute without the argument under its name as written.
    private static CheckIdKey KeyOfCheckId(AttributeSyntax attribute)
    {
        if (Argument(attribute, "CheckId", 1) is not { } expression)
        {
            return new CheckIdKey(attribute.Name.ToString().Trim(), IsRuleId: false, Written: false);
        }

        ExpressionSyntax inner = expression;
        while (inner is ParenthesizedExpressionSyntax parenthesized)
        {
            inner = parenthesized.Expression;
        }

        if (inner.IsKind(SyntaxKind.StringLiteralExpression))
        {
            string id = SuppressionReason.NormalizeId(((LiteralExpressionSyntax)inner).Token.ValueText.Split(':')[0].Trim());
            return id.Length > 0 ? new CheckIdKey(id, IsRuleId: true, Written: true) : new CheckIdKey(inner.ToString().Trim(), IsRuleId: false, Written: true);
        }

        return new CheckIdKey(expression.ToString().Trim(), IsRuleId: false, Written: true);
    }

    // ---- [GeneratedCode] ----

    private static IEnumerable<(TextSpan Span, Draft Value)> GeneratedCodes(SyntaxTree tree, SourceLines lines) =>
        GeneratedCode.FindAttributes(tree.GetRoot())
            .Select(attribute => (FormSpan(attribute), new Draft("'[GeneratedCode]'", ReasonComment, [GeneratedKey], [], SameLineComments(attribute, lines))));

    // The form starts at the bracket when the attribute is the first of its list, otherwise at the attribute itself.
    private static TextSpan FormSpan(AttributeSyntax attribute)
    {
        int start = attribute.Parent is AttributeListSyntax list && list.Attributes[0] == attribute ? list.SpanStart : attribute.SpanStart;
        return TextSpan.FromBounds(start, attribute.Span.End);
    }

    // The comments on the lines of the attribute list, also those after the list on its last line.
    private static string SameLineComments(AttributeSyntax attribute, SourceLines lines)
    {
        SyntaxNode list = attribute.Parent ?? attribute;
        int first = lines.Locate(list.SpanStart).Line;
        int last = lines.Locate(list.Span.End).Line;
        List<string> comments = [];
        for (SyntaxToken token = list.GetFirstToken(); !token.IsKind(SyntaxKind.None) && lines.Locate(token.SpanStart).Line <= last; token = token.GetNextToken())
        {
            comments.AddRange(token.LeadingTrivia.Concat(token.TrailingTrivia)
                .Where(trivia => IsComment(trivia) && lines.Locate(trivia.SpanStart).Line >= first && lines.Locate(trivia.SpanStart).Line <= last)
                .Select(trivia => trivia.ToString()));
        }

        return string.Join(' ', comments);
    }

    // ---- Arguments of an attribute ----

    // The argument with the name (named by '=' or ':', ignoring case), or the unnamed one at the position among the unnamed.
    private static ExpressionSyntax? Argument(AttributeSyntax attribute, string name, int position)
    {
        int unnamed = 0;
        foreach (AttributeArgumentSyntax argument in attribute.ArgumentList?.Arguments ?? [])
        {
            string? given = argument.NameEquals?.Name.Identifier.ValueText ?? argument.NameColon?.Name.Identifier.ValueText;
            if (given is null ? unnamed++ == position : string.Equals(given, name, StringComparison.OrdinalIgnoreCase))
            {
                return argument.Expression;
            }
        }

        return null;
    }

    // A string literal, a parenthesized one or a concatenation of such is read as text; null and default are no text; any
    // other expression, such as a constant of another class, is opaque, and its source stands for the reason.
    private static string TextOf(ExpressionSyntax? expression) => expression switch
    {
        null => string.Empty,
        LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.NullLiteralExpression) || literal.IsKind(SyntaxKind.DefaultLiteralExpression) => string.Empty,
        LiteralExpressionSyntax literal => literal.Token.ValueText,
        ParenthesizedExpressionSyntax parenthesized => TextOf(parenthesized.Expression),
        BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression) => TextOf(binary.Left) + TextOf(binary.Right),
        _ => expression.ToString(),
    };

    private static bool IsComment(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);

    private sealed record CheckIdKey(string Key, bool IsRuleId, bool Written);

    private sealed record Draft(string Description, string ReasonPlace, IReadOnlyList<string> Keys, IReadOnlyList<string> RuleIds, string Reason);
}
