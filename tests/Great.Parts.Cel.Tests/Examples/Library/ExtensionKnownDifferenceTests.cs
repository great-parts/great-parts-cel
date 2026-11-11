using System.Linq;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The four entries of the register of known differences with cel-go that belong to the extension sets, each with
// the minimal case of the entry and the outcome of Great.Parts.Cel that the entry states (the outcome of cel-go is in
// the entry, and the differential run takes it from the oracle). The signature is that of the register.
//   string_index_of_string_int(string, string, int) we-error        'tacocat'.indexOf('a', 30): here an error, the
//                                                                   offset is past the end of the string, as the
//                                                                   case string_ext/value_errors/indexof_out_of_range
//                                                                   of cel-spec expects; cel-go gives -1
//   string_last_index_of_string_int(string, string, int) we-error   'tacocat'.lastIndexOf('a', 30): the same, as
//                                                                   the case lastindexof_out_of_range expects
//   string_to_cidr(string) stage-mismatch                           cidr('192.168.0.0/'): here the compilation
//                                                                   succeeds and the evaluation fails, as the cases
//                                                                   of network_ext with constant arguments expect;
//                                                                   cel-go checks the constant and refuses it at
//                                                                   compilation, "invalid cidr argument"
//   string_to_ip(string) stage-mismatch                             ip('192.168.0.1.0'): the same for ip, "invalid
//                                                                   ip argument" in cel-go
// The offset that is past the end of the string is an offset greater than its length in code points: the offset equal
// to the length is no error and gives what cel-go gives (-1; the offset itself for an empty substring), and so does
// every offset below it. The empty substring does not change the rule: an offset greater than the length is the error
// there too, where cel-go clamps the offset and gives a value. The boundary is on both sides of 7 for 'tacocat'.
[Trait("Requirement", "R-6")]
[Trait("Requirement", "F-1")]
public sealed class ExtensionKnownDifferenceTests
{
    private static readonly string[] Signatures =
    [
        "string_index_of_string_int(string, string, int) we-error",
        "string_last_index_of_string_int(string, string, int) we-error",
        "string_to_cidr(string) stage-mismatch",
        "string_to_ip(string) stage-mismatch",
    ];

    // The signature, then the row of the table of error rows (ExtensionRows) that is the outcome of the entry.
    private static readonly (string Signature, string Row)[] Entries =
    [
        ("string_index_of_string_int(string, string, int) we-error", "Evaluate | 'tacocat'.indexOf^('a', 30) | IndexOutOfRange | index out of range: 30"),
        ("string_last_index_of_string_int(string, string, int) we-error", "Evaluate | 'tacocat'.lastIndexOf^('a', 30) | IndexOutOfRange | index out of range: 30"),
        ("string_to_cidr(string) stage-mismatch", "Evaluate | cidr^('192.168.0.0/') | InvalidArgument | invalid argument to 'cidr': *"),
        ("string_to_ip(string) stage-mismatch", "Evaluate | ip^('192.168.0.1.0') | InvalidArgument | invalid argument to 'ip': *"),
    ];

    private const string Boundary = """
        'tacocat'.indexOf('a', 6) => -1
        'tacocat'.indexOf('a', 4) => 5
        'tacocat'.indexOf('a', 7) => -1
        'tacocat'.indexOf('', 7) => 7
        'tacocat'.lastIndexOf('a', 6) => 5
        'tacocat'.lastIndexOf('a', 7) => -1
        'tacocat'.lastIndexOf('', 7) => 7
        'tacocat'.indexOf('a', 0) => 1
        'héllo'.indexOf('l', 5) => -1
        'héllo'.indexOf('', 5) => 5
        """;

    private const string PastTheEnd = """
        Evaluate | 'tacocat'.indexOf^('a', 8) | IndexOutOfRange | index out of range: 8
        Evaluate | 'tacocat'.indexOf^('', 8) | IndexOutOfRange | index out of range: 8
        Evaluate | 'tacocat'.lastIndexOf^('a', 8) | IndexOutOfRange | index out of range: 8
        Evaluate | 'tacocat'.lastIndexOf^('', 8) | IndexOutOfRange | index out of range: 8
        Evaluate | 'héllo'.indexOf^('l', 6) | IndexOutOfRange | index out of range: 6
        Evaluate | 'tacocat'.indexOf^('a', 9223372036854775807) | IndexOutOfRange | index out of range: 9223372036854775807
        Evaluate | s.indexOf^('a', 30) | IndexOutOfRange | index out of range: 30
        Evaluate | s.lastIndexOf^('a', 30) | IndexOutOfRange | index out of range: 30
        """;

    // The argument is a constant or in a variable: the evaluation says no, and the compilation does not look at it.
    private const string ConstantAndVariable = """
        Evaluate | ip^('192.168.0.1.0') | InvalidArgument | invalid argument to 'ip': *
        Evaluate | ip^(s) | InvalidArgument | invalid argument to 'ip': *
        Evaluate | cidr^('10.0.0.0/33') | InvalidArgument | invalid argument to 'cidr': *
        Evaluate | cidr^(s) | InvalidArgument | invalid argument to 'cidr': *
        Evaluate | ip^('::ffff:1.2.3.4') | InvalidArgument | invalid argument to 'ip': *
        Evaluate | ip^('fe80::1%en0') | InvalidArgument | invalid argument to 'ip': *
        Evaluate | ip^('') | InvalidArgument | invalid argument to 'ip': *
        Evaluate | cidr^('fe80::1%en0/64') | InvalidArgument | invalid argument to 'cidr': *
        Evaluate | cidr^('::ffff:1.2.3.4/96') | InvalidArgument | invalid argument to 'cidr': *
        Evaluate | cidr^('') | InvalidArgument | invalid argument to 'cidr': *
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Strings(5))
        .AddExtension(CelExtensions.Network(1))
        .AddVariable<string>("s")
        .Build()
        .GetEnvironmentOrThrow();

    // The variable s holds 'tacocat': it is no address and no CIDR, and it is the string that the rows with a variable
    // search in.
    private readonly CelBindings bindings = new CelBindings().Add("s", "tacocat");

    [Fact]
    public void EveryEntry_IsTheOutcomeOfTheRegister()
    {
        var failures = Entries
            .SelectMany(entry => ExtensionRows.CheckErrors(environment, entry.Row, bindings).Select(failure => entry.Signature + ": " + failure))
            .ToList();

        Assert.Empty(failures);
    }

    [Fact]
    public void EveryEntryOfTheRegister_HasAnExample()
    {
        Assert.Equal(Signatures.Order(System.StringComparer.Ordinal), Entries.Select(entry => entry.Signature).Order(System.StringComparer.Ordinal));
    }

    [Fact]
    public void OffsetUpToTheLength_GivesWhatCelGoGives()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, Boundary, bindings));
    }

    [Fact]
    public void OffsetGreaterThanTheLength_IsAnErrorAlsoForTheEmptySubstring()
    {
        Assert.Empty(ExtensionRows.CheckErrors(environment, PastTheEnd, bindings));
    }

    [Fact]
    public void ConstantArgumentOfIpAndCidr_IsAnErrorOfEvaluationNotOfCompilation()
    {
        Assert.Empty(ExtensionRows.CheckErrors(environment, ConstantAndVariable, bindings));
    }
}
