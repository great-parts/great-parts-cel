# CEL rules in FluentValidation

An application that already validates its objects with [FluentValidation](https://docs.fluentvalidation.net/) can keep its validators and let some rules be CEL texts, which live in a database or a configuration file and change without a release. This recipe shows how: one method, `Cel(...)`, on the rule builder of FluentValidation.

The libraries of Great.Parts.Cel do not depend on FluentValidation, and there is no package that does. FluentValidation changes its API between major versions, so the code is a sample to copy into your project, not a package to reference. The runnable sample is the project `samples/FluentValidation`, written for FluentValidation 12.2.0; the method is in [CelRuleBuilderExtensions.cs](../../samples/FluentValidation/CelRuleBuilderExtensions.cs) and the validators are in [Program.cs](../../samples/FluentValidation/Program.cs). The code is not shown on this page in full, so that the page does not go out of date with the sample; the build of the samples compiles and runs the sample.

## What a rule looks like

A rule is a text of the type `bool` over one variable, `value`, the property that the rule checks, and a message for the failure:

```cs
RuleFor(order => order.Total).Cel(environment, "value > 0.0 && value <= 10000.0", "The total is above 0 and at most 10000.");
RuleFor(order => order.Customer).Cel(environment, "value.startsWith('C-')", "The code of the customer starts with C-.");
```

The environment is the CEL environment of the application, built once as in [Getting started](../getting-started.md). The method gives it the variable `value`, of the type of the property, in a derived environment (`CelEnvironment.ToBuilder`), so the environment of the application does not need to declare `value` and one environment serves properties of any type. The signature of the method:

```cs
public static IRuleBuilderOptions<T, TProperty> Cel<T, TProperty>(
    this IRuleBuilder<T, TProperty> ruleBuilder, CelEnvironment environment, string expression, string message)
```

It returns `IRuleBuilderOptions<T, TProperty>`, so `When` and `Unless` work on the rule as on any other.

## What each result becomes

| The rule | In FluentValidation |
| :- | :- |
| gives `true` | no failure |
| gives `false` | a `ValidationFailure` with your message and the property name; `ErrorCode` is `null` |
| fails with an error when it is evaluated, for example a division by zero | a `ValidationFailure` with your message and the property name; `ErrorCode` is the name of the `CelErrorCode`, here `DivisionByZero` |
| does not compile | `CelException` from `Cel(...)`, that is, when the validator is created; its `Errors` list every error |

An error of the evaluation is a failure, never an exception and never a pass: a rule that cannot be evaluated does not let the object through. A test of the application compares `ErrorCode` with the name of a member of `CelErrorCode`; see [Error codes](../error-codes.md). The text of the error is not copied into the failure. If the application needs it, the method can put `result.Errors[0].Message` into `CustomState` of the failure.

A rule that does not compile is found when the validator is created. Create the validators at the start of the application, or in a test, and a wrong text does not wait for the first object to be validated.

## The sample prints

```text
valid: True
Customer: The code of the customer starts with C-. (error code: none)
Total: The total is above 0 and at most 10000. (error code: none)
Quantity: The quantity is between 1 and 100. (error code: DivisionByZero)
not created: UndeclaredReference
```

The first line is an order that passes. The next three are one order that fails all three rules: the first two rules give `false`, the third is `100 / value >= 1` with `value` equal to 0. The last line is a validator whose rule reads `limit`, which no environment declares.

## Adapt it

- **One property is not enough.** Give the rule the whole object: `RuleFor(order => order).Cel(environment, "value.total > value.limit", "...")` reads the fields of the object, if the environment has a type context that describes the class, as in section 3 of [Getting started](../getting-started.md). `value` is then an object of the application.
- **Types of the property.** `TProperty` is declared by `AddVariable<TProperty>`, so it is a type of [Type mapping](../type-mapping.md): numbers, strings, time, lists and dictionaries of them, and the objects that a type context describes. For another type the method fails when the validator is created, with the build error of the environment.
- **A function that waits.** `Must` is synchronous, and so is the method. A rule that calls an asynchronous function of the application needs `MustAsync` and `EvaluateAsync`; see [Application functions](application-functions.md). A synchronous evaluation of such a rule is the failure with `ErrorCode` equal to `AsyncEvaluationRequired`.
- **Where the failure is added.** FluentValidation adds a failure when the predicate of `Must` gives `false`, with a message of its own and no way to set `ErrorCode` from the result. The method adds its failure with `context.AddFailure` and lets the predicate pass, so the object gets exactly one failure per rule. The price is that `WithMessage` and `WithErrorCode` on this rule do nothing: the message is the argument of `Cel`.
- **Cost.** The evaluation uses the cost limit of the environment, if the environment has one, as every evaluation does; see [Cost](../cost.md). Call `SetDefaultCostLimit` on the builder of the environment when the texts come from users.
