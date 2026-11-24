using Great.Parts.Cel;

// A function of the application is a .NET delegate. The CEL types of its parameters and of its result come from the .NET types:
// double is double, long is int, string is string and IReadOnlyList<string> is list(string).
CelEnvironment environment = CelEnvironment.Create(builder => builder
    .AddVariable<string>("email")
    .AddVariable<double>("total")
    .AddVariable<long>("years")
    // A function is called as discount(total, years).
    .AddFunction("discount", (double price, long loyalty) => price * Math.Min(loyalty, 5L) / 100.0)
    // A method takes the value before the dot as its first parameter: email.domain() and email.endsWithAny(['.corp', '.gov']).
    .AddMethod("domain", (string address) => address[(address.IndexOf('@') + 1)..])
    .AddMethod("endsWithAny", (string text, IReadOnlyList<string> suffixes) =>
        suffixes.Any(suffix => text.EndsWith(suffix, StringComparison.Ordinal))));

CelProgram<double> payable = environment
    .Compile<double>("total - discount(total, years)")
    .GetProgramOrThrow();
CelProgram<bool> business = environment
    .Compile<bool>("email.domain() == 'example.com' || email.endsWithAny(['.corp', '.gov'])")
    .GetProgramOrThrow();

CelBindings first = new CelBindings().Add("email", "ada@example.com").Add("total", 200.0).Add("years", 3L);
CelBindings second = new CelBindings().Add("email", "eve@mail.test").Add("total", 80.0).Add("years", 9L);

Console.WriteLine(payable.Evaluate(first).GetValueOrThrow()); // Output: 194
Console.WriteLine(business.Evaluate(first).GetValueOrThrow()); // Output: True
Console.WriteLine(payable.Evaluate(second).GetValueOrThrow()); // Output: 76
Console.WriteLine(business.Evaluate(second).GetValueOrThrow()); // Output: False
