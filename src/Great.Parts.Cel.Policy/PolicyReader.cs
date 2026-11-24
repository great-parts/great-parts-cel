using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Policy.Yaml;

namespace Great.Parts.Cel.Policy;

// The reader of the format: it walks the nodes of a YAML document as the parser of policies of cel-go (policy/parser.go) does, and
// builds the model of the policy. Every error of the structure is collected with its position and the walk goes on, so a document with
// several errors reports them all; the errors are ordered by position at the end. Nested rules are not read by recursion: a rule or a
// match whose nodes are still to be read waits on a stack, so the depth of a document costs no stack.
internal sealed partial class PolicyReader
{
    // The number of rules, matches and variables the reader builds before it gives up. An alias is the same node at every place it
    // is used, so a small document can stand for an enormous policy; the limit of the compilation (100 variables and nested rules)
    // is far below this one and is checked by the compilation, as cel-go checks it.
    private const int MaxWalked = 100_000;

    // The budget of the reader for text, in units, may be this many times the length of the document, plus the allowance. A unit is a
    // character of a string that the reader builds and a place in the document that such a string keeps for an escape sequence or a
    // line break (its segments), and a character of a key or a tag that an error repeats in its message. An alias stands for its node
    // at every place it is used, so the limit of the number of rules does not bound the memory of the strings and of the messages, nor
    // the text that the compilation parses; a policy without aliases and without errors never reaches the limit, since its strings
    // are parts of the document.
    private const int TextFactor = 4;
    private const int TextAllowance = 65_536;

    private const string TextLimitMessage = "the strings of the policy are longer than four times the document";

    // The types of YAML nodes the format knows, by the tag of the node.
    private enum NodeType
    {
        Text,
        Str,
        Null,
        Bool,
        Int,
        Float,
        Seq,
        Map,
        Timestamp,
    }

    // A rule or a match whose entries are still to be read.
    private readonly record struct Work(YamlNode Node, PolicyRule? Rule, PolicyMatch? Match);

    private readonly string _text;
    private readonly string _sourceName;
    private readonly LineTable _lines;
    private readonly List<(int Offset, int Sequence, CelError Error)> _errors = [];
    private readonly Stack<Work> _work = new();
    private readonly long _textLimit;
    private int _walked;
    private long _textSpent;
    private bool _stopped;

    private PolicyReader(string text, string sourceName)
    {
        _text = text;
        _textLimit = ((long)TextFactor * text.Length) + TextAllowance;
        _sourceName = sourceName;
        _lines = new LineTable(text);
    }

    // Reads a document into a policy, or into all the errors of the document.
    internal static CelPolicyParseResult Read(string text, string sourceName) => new PolicyReader(text, sourceName).Run();

    private CelPolicyParseResult Run()
    {
        YamlReadResult yaml = YamlReader.Read(_text);
        if (yaml.IsError)
        {
            int offset = Math.Clamp(yaml.ErrorOffset, 0, _text.Length);
            AddError(CelErrorCode.InvalidYaml, offset, 0, yaml.ErrorMessage!, null);
            return Failure();
        }

        PolicyDocument? document = null;
        if (yaml.Root is null)
        {
            // yaml.Unmarshal leaves the node empty for a stream without a document.
            AddError(CelErrorCode.InvalidPolicy, 0, 0, "got yaml node of kind 0, wanted mapping node", null);
        }
        else
        {
            document = ReadDocument(yaml.Root);
            while (_work.Count > 0 && !_stopped)
            {
                Work work = _work.Pop();
                if (work.Rule is not null)
                {
                    ReadRule(work.Node, work.Rule);
                }
                else
                {
                    ReadMatch(work.Node, work.Match!);
                }
            }
        }

        if (_errors.Count > 0 || document is null)
        {
            return Failure();
        }

        return new CelPolicyParseResult(new CelPolicy(_text, _sourceName, document));
    }

    private CelPolicyParseResult Failure()
    {
        _errors.Sort(static (a, b) => a.Offset != b.Offset ? a.Offset.CompareTo(b.Offset) : a.Sequence.CompareTo(b.Sequence));
        ImmutableArray<CelError>.Builder errors = ImmutableArray.CreateBuilder<CelError>(_errors.Count);
        foreach ((int _, int _, CelError error) in _errors)
        {
            errors.Add(error);
        }

        return new CelPolicyParseResult(errors.MoveToImmutable());
    }

    // ---- The document ----

    private PolicyDocument ReadDocument(YamlNode root)
    {
        var document = new PolicyDocument(root);
        if (AssertType(root, NodeType.Map) is null || root is not YamlMapping mapping)
        {
            return document;
        }

        foreach (YamlPair pair in mapping.Pairs)
        {
            string name = KeyText(pair.Key);
            switch (name)
            {
                case "imports":
                    ReadImports(document, pair.Value);
                    break;
                case "name":
                    document.Name = NewString(pair.Key, pair.Value);
                    break;
                case "description":
                    document.Description = StrictString(pair.Value);
                    break;
                case "rule":
                    document.Rule = StartRule(pair.Value);
                    break;
                default:
                    ReportNamed(pair.Key, "unsupported policy tag: ", name);
                    break;
            }
        }

        return document;
    }

    private void ReadImports(PolicyDocument document, YamlNode node)
    {
        if (AssertType(node, NodeType.Seq) is null || node is not YamlSequence sequence)
        {
            return;
        }

        foreach (YamlNode item in sequence.Items)
        {
            var import = new PolicyImport(item);
            document.Imports.Add(import);
            if (AssertType(item, NodeType.Map) is null || item is not YamlMapping mapping)
            {
                continue;
            }

            foreach (YamlPair pair in mapping.Pairs)
            {
                // Another key of an import is not an error.
                if (string.Equals(KeyText(pair.Key), "name", StringComparison.Ordinal))
                {
                    import.Name = NewString(pair.Key, pair.Value);
                }
            }
        }
    }

    // ---- Rules, variables and matches ----

    // A rule is created at once and read later, from the stack.
    private PolicyRule? StartRule(YamlNode node)
    {
        if (!Count(node))
        {
            return null;
        }

        var rule = new PolicyRule(node);
        _work.Push(new Work(node, rule, null));
        return rule;
    }

    private void ReadRule(YamlNode node, PolicyRule rule)
    {
        if (AssertType(node, NodeType.Map) is null || node is not YamlMapping mapping)
        {
            return;
        }

        foreach (YamlPair pair in mapping.Pairs)
        {
            ReadRuleEntry(rule, pair.Key, pair.Value);
        }
    }

    private void ReadRuleEntry(PolicyRule rule, YamlNode key, YamlNode value)
    {
        string name = KeyText(key);
        switch (name)
        {
            case "id":
                rule.Id = NewString(key, value);
                break;
            case "description":
                rule.Description = NewString(key, value);
                break;
            case "variables":
                ReadVariables(rule, value);
                break;
            case "match" or "aggregate":
                ReadMatches(rule, key, name, value);
                break;
            default:
                ReportNamed(key, "unsupported rule tag: ", name);
                break;
        }
    }

    private void ReadVariables(PolicyRule rule, YamlNode node)
    {
        if (AssertType(node, NodeType.Seq) is null || node is not YamlSequence sequence)
        {
            return;
        }

        foreach (YamlNode item in sequence.Items)
        {
            if (!Count(item))
            {
                return;
            }

            var variable = new PolicyVariable(item);
            rule.Variables.Add(variable);
            if (AssertType(item, NodeType.Map) is not null && item is YamlMapping mapping)
            {
                ReadVariable(variable, mapping);
            }
        }
    }

    private void ReadVariable(PolicyVariable variable, YamlMapping mapping)
    {
        foreach (YamlPair pair in mapping.Pairs)
        {
            string name = KeyText(pair.Key);
            switch (name)
            {
                case "name":
                    variable.Name = NewString(pair.Key, pair.Value);
                    break;
                case "expression":
                    variable.Expression = NewString(pair.Key, pair.Value);
                    break;
                default:
                    ReportNamed(pair.Key, "unsupported variable tag: ", name);
                    break;
            }
        }
    }

    // "match" is the first-match semantic and "aggregate" the aggregate one; a rule has one of them.
    private void ReadMatches(PolicyRule rule, YamlNode key, string name, YamlNode node)
    {
        PolicySemantic semantic = name == "aggregate" ? PolicySemantic.Aggregate : PolicySemantic.FirstMatch;
        if (rule.Semantic != PolicySemantic.Unspecified && rule.Semantic != semantic)
        {
            Report(key, "Only one of 'match' or 'aggregate' may be set in a rule", name);
            return;
        }

        rule.Semantic = semantic;
        if (AssertType(node, NodeType.Seq) is null || node is not YamlSequence sequence)
        {
            return;
        }

        foreach (YamlNode item in sequence.Items)
        {
            if (!Count(item))
            {
                return;
            }

            var match = new PolicyMatch(item);
            rule.Matches.Add(match);
            _work.Push(new Work(item, null, match));
        }
    }

    private void ReadMatch(YamlNode node, PolicyMatch match)
    {
        if (AssertType(node, NodeType.Map) is null || node is not YamlMapping mapping)
        {
            return;
        }

        foreach (YamlPair pair in mapping.Pairs)
        {
            ReadMatchEntry(match, pair.Key, pair.Value);
        }

        if (match.Output is null && match.Rule is null)
        {
            Report(node, "match does not specify a rule or output", null);
        }
    }

    private void ReadMatchEntry(PolicyMatch match, YamlNode key, YamlNode value)
    {
        string name = KeyText(key);
        switch (name)
        {
            case "condition":
                match.Condition = NewString(key, value);
                break;
            case "output":
                if (match.Rule is not null)
                {
                    Report(key, "only the rule or the output may be set", name);
                }

                match.Output = NewString(key, value);
                break;
            case "explanation":
                if (match.Rule is not null)
                {
                    Report(key, "explanation can only be set on output match cases, not nested rules", name);
                }

                match.Explanation = NewString(key, value);
                break;
            case "rule" or "match" or "aggregate":
                ReadNestedRule(match, key, name, value);
                break;
            default:
                ReportNamed(key, "unsupported match tag: ", name);
                break;
        }
    }

    // A nested rule is the value of "rule", and, as in cel-go, of "match" and "aggregate" directly under a match.
    private void ReadNestedRule(PolicyMatch match, YamlNode key, string name, YamlNode value)
    {
        if (match.Output is not null)
        {
            Report(key, "only the rule or the output may be set", name);
        }

        if (match.Explanation is not null)
        {
            Report(key, "explanation can only be set on output match cases, not nested rules", name);
        }

        match.Rule = StartRule(value);
    }

    // Counts a rule, a match or a variable that is built; false when the reader gives up.
    private bool Count(YamlNode node)
    {
        if (_stopped)
        {
            return false;
        }

        _walked++;
        if (_walked <= MaxWalked)
        {
            return true;
        }

        (int offset, int length) = Place(node);
        GiveUp(offset, length, "the policy has more than 100000 rules, matches and variables");
        return false;
    }

    // Charges units of text to the budget; false when the budget is passed.
    private bool Spend(long units)
    {
        _textSpent += units;
        return _textSpent <= _textLimit;
    }

    // The one error of a limit, at its place: the reader reads and reports nothing more.
    private void GiveUp(int offset, int length, string message)
    {
        Append(CelErrorCode.InvalidPolicy, offset, length, message, null);
        _stopped = true;
    }
}
