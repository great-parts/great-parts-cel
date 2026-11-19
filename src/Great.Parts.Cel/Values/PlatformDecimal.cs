// The type that holds a CEL decimal on the target: a Decimal128 on net11.0, a System.Decimal on net10.0. The rules of the set —
// reading, text, conversions, cohort, equality and order — are one code for both, written over DecimalParts; DecimalPlatform has
// the few operations that differ by target.
#if NET11_0_OR_GREATER
global using PlatformDecimal = System.Numerics.Decimal128;
#else
global using PlatformDecimal = decimal;
#endif
