using FluentValidation;
using FluentValidation.Results;
using Great.Parts.Cel;
using Great.Parts.Cel.Diagnostics;

// The standard library is enough here: a rule reads only the value of the property that it checks.
CelEnvironment environment = CelEnvironment.Create(_ => { });

OrderValidator validator = new(environment);

ValidationResult accepted = validator.Validate(new Order("C-17", 250.0, 5));
Console.WriteLine($"valid: {accepted.IsValid}");

// A rule gives false, or fails with an error: a division by zero. The error code of a failure is the name of the CelErrorCode.
ValidationResult rejected = validator.Validate(new Order("17", 0.0, 0));
foreach (ValidationFailure failure in rejected.Errors)
{
    Console.WriteLine($"{failure.PropertyName}: {failure.ErrorMessage} (error code: {failure.ErrorCode ?? "none"})");
}

// A rule that does not compile is found when the validator is created, not when the first object is validated.
try
{
    _ = new BrokenValidator(environment);
}
catch (CelException exception)
{
    Console.WriteLine($"not created: {exception.Errors[0].Code}");
}

public sealed record Order(string Customer, double Total, long Quantity);

public sealed class OrderValidator : AbstractValidator<Order>
{
    public OrderValidator(CelEnvironment environment)
    {
        RuleFor(order => order.Customer).Cel(environment, "value.startsWith('C-')", "The code of the customer starts with C-.");
        RuleFor(order => order.Total).Cel(environment, "value > 0.0 && value <= 10000.0", "The total is above 0 and at most 10000.");
        RuleFor(order => order.Quantity).Cel(environment, "100 / value >= 1", "The quantity is between 1 and 100.");
    }
}

public sealed class BrokenValidator : AbstractValidator<Order>
{
    public BrokenValidator(CelEnvironment environment)
    {
        RuleFor(order => order.Total).Cel(environment, "value > limit", "The total is within the limit.");
    }
}
