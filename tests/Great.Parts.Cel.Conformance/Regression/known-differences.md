# Known differences from cel-go

Divergences of Great.Parts.Cel from the pinned cel-go version that are analysed and are not counted as bugs of Great.Parts.Cel. Every entry has a signature, a class (`cel-go-bug` or `by-design`) and a reference: a link to the language definition or a minimal case.

| Signature | Class | Reference |
|---|---|---|
| map(bool, int, bool, int) we-error | cel-go-bug | {true: 1, true: 3} fails with a duplicate map key error: the language definition makes duplicate keys an error, and the cel-spec case fields/qualified_identifier_resolution/map_value_repeat_key expects one; cel-go returns {true: 3}, the last entry wins. |
| map(double, int) we-error | cel-go-bug | {1.0: 5} fails with an error: map keys are int, uint, bool or string by the language definition, and the cel-spec case fields/qualified_identifier_resolution/map_key_float expects an error; cel-go returns a map with a double key. |
| map(int, int, uint, int) we-error | cel-go-bug | {0: 1, 0u: 2} fails with a duplicate map key error, because 0 and 0u are equal under numeric equality; the cel-spec case fields/qualified_identifier_resolution/map_value_repeat_key_heterogeneous expects an error; cel-go returns a map with two entries. |
| map(null_type, bool) we-error | cel-go-bug | {null: false} fails with an error: map keys are int, uint, bool or string by the language definition, and the cel-spec case fields/qualified_identifier_resolution/map_key_null expects an error; cel-go returns a map with a null key. |
