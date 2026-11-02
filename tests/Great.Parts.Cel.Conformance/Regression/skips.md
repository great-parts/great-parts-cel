# Regression corpus skip list

Cases of the regression corpus that may fail in the given scope. Every entry names the divergence signature the case minimizes, the verdict on it, its severity, a reason and, before a release, an issue.

| Case | Scope | Signature | Verdict | Severity | Reason | Issue |
|---|---|---|---|---|---|---|
| map_literal/duplicate_key/bool_key_repeated | * * * | map(bool, int, bool, int) we-error | cel-go-bug | S2 | The library fails a map literal with a repeated key with a duplicate map key error, as the language definition requires; cel-go returns the map of the last entry. | |
| map_literal/duplicate_key/int_and_uint_key_equal | * * * | map(int, int, uint, int) we-error | cel-go-bug | S2 | The library fails a map literal whose keys 0 and 0u are equal under numeric equality with a duplicate map key error; cel-go returns a map of two entries. | |
| map_literal/unsupported_key/double_key | * * * | map(double, int) we-error | cel-go-bug | S2 | The library fails a map literal with a double key, since a map key is an int, uint, bool or string; cel-go returns a map with a double key. | |
| map_literal/unsupported_key/null_key | * * * | map(null_type, bool) we-error | cel-go-bug | S2 | The library fails a map literal with a null key, since a map key is an int, uint, bool or string; cel-go returns a map with a null key. | |
