using Great.Parts.Cel;
using Great.Parts.Cel.Diagnostics;

// The variables are the properties of an anonymous object; the type argument is the type of the result.
long total = CelExpression.Evaluate<long>("price * quantity", new { price = 12L, quantity = 3L });
Console.WriteLine(total); // Output: 36

bool allowed = CelExpression.Evaluate<bool>(
    "age >= 18 && country in ['DE', 'FR']",
    new { age = 30L, country = "FR" });
Console.WriteLine(allowed); // Output: True

// A dictionary does the same when the names are known only at run time.
var variables = new Dictionary<string, object?> { ["name"] = "Ada", ["roles"] = new[] { "admin", "dev" } };
Console.WriteLine(CelExpression.Evaluate<bool>("name == 'Ada' && 'admin' in roles", variables)); // Output: True

// A wrong expression is a CelException that carries every error.
try
{
    _ = CelExpression.Evaluate<long>("price * quantity", new { price = 12L });
}
catch (CelException exception)
{
    Console.WriteLine(exception.Errors[0].Code); // Output: UndeclaredReference
}
