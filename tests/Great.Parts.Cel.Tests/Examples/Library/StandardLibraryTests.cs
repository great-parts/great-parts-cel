using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The functions of the standard library, version 1, of this part: logic, equality, arithmetic, ordering,
// index, in, size, strings, type and the conversions. Each row is "expression => expected": both sides
// are evaluated, and the value of the expression must have the kind of the expected value and be equal
// to it by CEL equality; NaN stands for itself. The value of every row is that of cel-go v0.32.0.
// Operands of the static type dyn reach the overloads that type checking only allows with dyn, such as
// the ordering across numeric kinds. The rows that Values/EqualityTests, OrderingTests and MapKeyTests
// already state are not repeated here.
[Trait("Requirement", "F-1")]
public sealed class StandardLibraryTests
{
    private const string Rows = """
        true && false => false
        false && 1 / 0 > 0 => false
        1 / 0 > 0 && false => false
        1 / 0 > 0 || true => true
        true || 1 / 0 > 0 => true
        !true => false
        true ? 1 : 1 / 0 => 1
        false ? 1 / 0 : 2 => 2
        'a' != 'b' => true
        type(1u) == uint => true
        type(1.0) == double => true
        type(true) == bool => true
        type('a') == string => true
        type(b'') == bytes => true
        type(int) == type => true
        2 * 3 => 6
        7 % 3 => 1
        -7 % 3 => -1
        7 % -3 => 1
        -7 / 2 => -3
        5u - 3u => 2u
        3u * 4u => 12u
        7u / 2u => 3u
        7u % 2u => 1u
        1.5 + 2.25 => 3.75
        5.0 - 0.5 => 4.5
        2.0 * 3.0 => 6.0
        -(1.5) => -1.5
        1.0 / 0.0 => double('inf')
        -1.0 / 0.0 => double('-inf')
        0.0 / 0.0 => double('NaN')
        1 < 2 => true
        2u > 1u => true
        1 < dyn(2u) => true
        dyn(-1) < 1u => true
        dyn(2u) > -1.5 => true
        dyn(1.0) <= 1 => true
        'a' < 'B' => false
        '' < '\U0001F600' => true
        b'\x01' < b'\x02' => true
        1.0 >= 1.0 => true
        double('NaN') < 1.0 => false
        double('NaN') >= 1.0 => false
        'abc' <= 'abc' => true
        2 >= 3 => false
        [1, 2, 3][1] => 2
        [1, 2][dyn(1u)] => 2
        [1, 2][dyn(1.0)] => 2
        {'a': 1}['a'] => 1
        2 in [1, 2] => true
        3 in [1, 2] => false
        'a' in {'a': 1} => true
        dyn(2u) in [1, 2] => true
        size([1, 2]) => 2
        [1].size() => 1
        size({'a': 1}) => 1
        {}.size() => 0
        size('héllo') => 5
        size('\U0001F600') => 1
        'abc'.size() => 3
        size(b'abc') => 3
        b'abc'.size() => 3
        'abc' + 'def' => 'abcdef'
        b'ab' + b'c' => b'abc'
        [1] + [2, 3] => [1, 2, 3]
        'hello'.contains('ell') => true
        'hello'.contains('') => true
        ''.contains('a') => false
        'hello'.startsWith('he') => true
        'hello'.endsWith('lo') => true
        'hello'.endsWith('') => true
        int(1.9) => 1
        int(-1.9) => -1
        int(5u) => 5
        int('-42') => -42
        int('+5') => 5
        uint(5) => 5u
        uint(1.5) => 1u
        uint('42') => 42u
        uint(-0.0) => 0u
        double(5) => 5.0
        double(5u) => 5.0
        double('1.5') => 1.5
        double('1_000.5') => 1000.5
        double('0x1p-2') => 0.25
        double('inf') > 1e308 => true
        double('-Infinity') < -1e308 => true
        string(-1) => '-1'
        string(5u) => '5'
        string(1.5) => '1.5'
        string(1e6) => '1e+06'
        string(123456.0) => '123456'
        string(-0.0) => '-0'
        string(1e-5) => '1e-05'
        string(1e21) => '1e+21'
        string(0.1) => '0.1'
        string(double('inf')) => '+Inf'
        string(true) => 'true'
        string(b'abc') => 'abc'
        bytes('abc') => b'abc'
        bytes('é') => b'\xc3\xa9'
        bool('true') => true
        bool('T') => true
        bool('0') => false
        bool('False') => false
        int(1) => 1
        uint(1u) => 1u
        double(1.5) => 1.5
        bool(true) => true
        string('a') => 'a'
        bytes(b'a') => b'a'
        dyn(1) + 1 => 2
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    [Fact]
    public void EveryRow_GivesItsValue()
    {
        var failures = new List<string>();
        foreach (string row in Rows.Split('\n'))
        {
            string[] sides = row.Trim().Split(" => ");
            string? failure = Check(sides[0], sides[1]);
            if (failure is not null)
            {
                failures.Add(row.Trim() + ": " + failure);
            }
        }

        Assert.Empty(failures);
    }

    private string? Check(string expression, string expected)
    {
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(expression);
        if (!compiled.IsSuccess)
        {
            return "compile error " + compiled.Errors[0].Code + ": " + compiled.Errors[0].Message;
        }

        CelEvalResult<CelValue> actual = compiled.GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);
        if (!actual.IsSuccess)
        {
            return "error " + actual.Errors[0].Code + ": " + actual.Errors[0].Message;
        }

        CelValue value = actual.GetValueOrThrow();
        CelValue wanted = environment.Compile<CelValue>(expected).GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();
        if (value.Kind != wanted.Kind)
        {
            return "kind " + value.Kind;
        }

        if (value.TryGetDouble(out double number) && wanted.TryGetDouble(out double wantedNumber) && double.IsNaN(number) && double.IsNaN(wantedNumber))
        {
            return null;
        }

        return CelValue.CelEquals(value, wanted).TryGetBoolean(out bool equal) && equal ? null : "another value";
    }
}
