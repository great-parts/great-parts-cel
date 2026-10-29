using System;
using Great.Parts.Cel.Syntax;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// Every expression of the cel-spec test files basic and plumbing parses into the syntax tree.
// The expressions are copied letter for letter from tests/cel-spec/tests/simple/testdata/basic.textproto
// and plumbing.textproto of the pinned cel-spec commit, after the escapes of the text format are
// decoded. The expected values of the cases are checked by the suite runner, not here.
[Trait("Requirement", "F-1")]
public sealed class CelSpecExpressionParseTests
{
    // One line per case: "<file>/<section>/<test> <expression>"; the expression is the rest of the line.
    private const string Cases = """""""
        basic/self_eval_zeroish/self_eval_int_zero 0
        basic/self_eval_zeroish/self_eval_uint_zero 0u
        basic/self_eval_zeroish/self_eval_uint_alias_zero 0U
        basic/self_eval_zeroish/self_eval_float_zero 0.0
        basic/self_eval_zeroish/self_eval_float_zerowithexp 0e+0
        basic/self_eval_zeroish/self_eval_string_empty ''
        basic/self_eval_zeroish/self_eval_string_empty_quotes ""
        basic/self_eval_zeroish/self_eval_string_raw_prefix r""
        basic/self_eval_zeroish/self_eval_bytes_empty b""
        basic/self_eval_zeroish/self_eval_bool_false false
        basic/self_eval_zeroish/self_eval_null null
        basic/self_eval_zeroish/self_eval_empty_list []
        basic/self_eval_zeroish/self_eval_empty_map {}
        basic/self_eval_zeroish/self_eval_string_raw_prefix_triple_double r""""""
        basic/self_eval_zeroish/self_eval_string_raw_prefix_triple_single r''''''
        basic/self_eval_nonzeroish/self_eval_int_nonzero 42
        basic/self_eval_nonzeroish/self_eval_uint_nonzero 123456789u
        basic/self_eval_nonzeroish/self_eval_uint_alias_nonzero 123456789U
        basic/self_eval_nonzeroish/self_eval_int_negative_min -9223372036854775808
        basic/self_eval_nonzeroish/self_eval_float_negative_exp -2.3e+1
        basic/self_eval_nonzeroish/self_eval_string_excl "!"
        basic/self_eval_nonzeroish/self_eval_string_escape '\''
        basic/self_eval_nonzeroish/self_eval_bytes_escape b'ÿ'
        basic/self_eval_nonzeroish/self_eval_bytes_invalid_utf8 b'\000\xff'
        basic/self_eval_nonzeroish/self_eval_list_singleitem [-1]
        basic/self_eval_nonzeroish/self_eval_map_singleitem {"k":"v"}
        basic/self_eval_nonzeroish/self_eval_bool_true true
        basic/self_eval_nonzeroish/self_eval_int_hex 0x55555555
        basic/self_eval_nonzeroish/self_eval_int_hex_negative -0x55555555
        basic/self_eval_nonzeroish/self_eval_uint_hex 0x55555555u
        basic/self_eval_nonzeroish/self_eval_uint_alias_hex 0x55555555U
        basic/self_eval_nonzeroish/self_eval_unicode_escape_four "\u270c"
        basic/self_eval_nonzeroish/self_eval_unicode_escape_eight "\U0001f431"
        basic/self_eval_nonzeroish/self_eval_ascii_escape_seq "\a\b\f\n\r\t\v\"\'\\"
        basic/variables/self_eval_bound_lookup x
        basic/variables/self_eval_unbound_lookup x
        basic/variables/unbound_is_runtime_error x || true
        basic/functions/binop 1 + 1
        basic/functions/unbound f_unknown(17)
        basic/reserved_const/false false
        basic/reserved_const/true true
        basic/reserved_const/null null
        plumbing/min/min_program 17
        plumbing/eval_results/error_result 1 / 0
        plumbing/eval_results/eval_map_results {"k1":"v1","k":"v"}
        plumbing/check_inputs/skip_check [17, 'pancakes']
        plumbing/eval_inputs/one_ignored_value_arg 'foo'
        """"""";

    private readonly CelEnvironment environment =
        new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    public static TheoryData<string, string> Expressions
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (string line in CaseLines())
            {
                int space = line.IndexOf(' ', StringComparison.Ordinal);
                data.Add(line[..space], line[(space + 1)..]);
            }

            return data;
        }
    }

    [Fact]
    public void CaseList_HasEveryCaseOfBothFiles()
    {
        string[] lines = CaseLines();

        Assert.Equal(42, Array.FindAll(lines, line => line.StartsWith("basic/", StringComparison.Ordinal)).Length);
        Assert.Equal(5, Array.FindAll(lines, line => line.StartsWith("plumbing/", StringComparison.Ordinal)).Length);
    }

    [Theory]
    [MemberData(nameof(Expressions))]
    public void CelSpecExpression_ParsesIntoSyntaxTree(string caseName, string expression)
    {
        ParseResult result = environment.Parse(expression);

        Assert.True(result.IsSuccess, caseName);
        Assert.NotNull(result.Expression);
        Assert.Empty(result.Errors);
    }

    // The line breaks of the source file do not reach the expressions.
    private static string[] CaseLines() => Cases.ReplaceLineEndings("\n").Split('\n');
}
