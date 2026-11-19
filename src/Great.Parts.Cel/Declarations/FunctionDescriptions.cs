using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Great.Parts.Cel.Declarations;

// The descriptions of the functions of the standard library version 1 and of the extension sets of the catalog: one English sentence for
// each function name, given to each of its overloads. The text is from the language definition and from the documentation of the set;
// it ends with a period and does not repeat the signature. The keys are the names of the library, never text of an expression.
// A table of a set passes its overloads through Describe when it is made, so the description is part of the definition of the set.
internal static class FunctionDescriptions
{
    private static readonly FrozenDictionary<string, string> Table = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // The standard library, version 1.
        ["size"] = "Returns the number of elements of a list or a map, the number of code points of a string or the number of bytes of a bytes value.",
        ["type"] = "Returns the type of a value as a type value.",
        ["bool"] = "Converts a string to a bool, or returns a bool as it is.",
        ["bytes"] = "Converts a string to its UTF-8 bytes, or returns bytes as they are.",
        ["double"] = "Converts an int, a uint or a string to a double, or returns a double as it is.",
        ["duration"] = "Converts a string such as \"1.5s\" to a duration, or returns a duration as it is.",
        ["dyn"] = "Treats a value as the dynamic type, which turns off type checking of the expression that uses it.",
        ["int"] = "Converts a uint, a double, a string, a timestamp or a duration to an int, or returns an int as it is.",
        ["string"] = "Converts a value to its text form.",
        ["timestamp"] = "Converts a string in RFC 3339 form or an int of seconds since the Unix epoch to a timestamp.",
        ["uint"] = "Converts an int, a double or a string to a uint, or returns a uint as it is.",
        ["contains"] = "Tells whether a string contains another string.",
        ["endsWith"] = "Tells whether a string ends with another string.",
        ["startsWith"] = "Tells whether a string starts with another string.",
        ["matches"] = "Tells whether a string contains a match of a regular expression in the RE2 syntax.",
        ["getFullYear"] = "Returns the year of a timestamp, in UTC or in the given time zone.",
        ["getMonth"] = "Returns the zero-based month of a timestamp, in UTC or in the given time zone.",
        ["getDayOfYear"] = "Returns the zero-based day of the year of a timestamp, in UTC or in the given time zone.",
        ["getDayOfMonth"] = "Returns the zero-based day of the month of a timestamp, in UTC or in the given time zone.",
        ["getDate"] = "Returns the one-based day of the month of a timestamp, in UTC or in the given time zone.",
        ["getDayOfWeek"] = "Returns the day of the week of a timestamp, from 0 for Sunday, in UTC or in the given time zone.",
        ["getHours"] = "Returns the hours of a timestamp, in UTC or in the given time zone, or the whole hours of a duration.",
        ["getMinutes"] = "Returns the minutes of a timestamp, in UTC or in the given time zone, or the whole minutes of a duration.",
        ["getSeconds"] = "Returns the seconds of a timestamp, in UTC or in the given time zone, or the whole seconds of a duration.",
        ["getMilliseconds"] = "Returns the milliseconds of a timestamp, in UTC or in the given time zone, or those of a duration.",

        // The set strings.
        ["charAt"] = "Returns the character of a string at a code point index, as a string.",
        ["indexOf"] = "Returns the code point index of the first occurrence of a substring, or -1 when there is none.",
        ["lastIndexOf"] = "Returns the code point index of the last occurrence of a substring, or -1 when there is none.",
        ["lowerAscii"] = "Returns a string with the ASCII letters in lower case.",
        ["upperAscii"] = "Returns a string with the ASCII letters in upper case.",
        ["replace"] = "Replaces the occurrences of a substring with another string, all of them or up to a limit.",
        ["split"] = "Splits a string at a separator into a list of strings, into all parts or up to a limit.",
        ["substring"] = "Returns the part of a string from a code point index, or between two code point indexes.",
        ["trim"] = "Returns a string without the white space at both ends.",
        ["join"] = "Joins the strings of a list into one string, with an optional separator.",
        ["format"] = "Formats a string with the placeholders of a format string filled from a list of arguments.",
        ["strings.quote"] = "Returns a string as a CEL string literal in double quotes, with the escapes of the language.",
        ["reverse"] = "Returns a string or a list with its elements in reverse order.",

        // The set math.
        ["math.ceil"] = "Returns the smallest whole double that is not less than the argument.",
        ["math.floor"] = "Returns the largest whole double that is not greater than the argument.",
        ["math.round"] = "Returns the nearest whole double, with halves rounded away from zero.",
        ["math.trunc"] = "Returns the whole part of a double, rounded toward zero.",
        ["math.isInf"] = "Tells whether a double is an infinity.",
        ["math.isNaN"] = "Tells whether a double is not a number.",
        ["math.isFinite"] = "Tells whether a double is neither an infinity nor not a number.",
        ["math.abs"] = "Returns the absolute value of a number.",
        ["math.sign"] = "Returns -1, 0 or 1 by the sign of a number.",
        ["math.bitAnd"] = "Returns the bitwise AND of two integers of the same kind.",
        ["math.bitOr"] = "Returns the bitwise OR of two integers of the same kind.",
        ["math.bitXor"] = "Returns the bitwise exclusive OR of two integers of the same kind.",
        ["math.bitNot"] = "Returns the bitwise complement of an integer.",
        ["math.bitShiftLeft"] = "Shifts the bits of an integer to the left by a number of places.",
        ["math.bitShiftRight"] = "Shifts the bits of an integer to the right by a number of places.",
        ["math.sqrt"] = "Returns the square root of a number as a double.",

        // The set lists.
        ["slice"] = "Returns the elements of a list from one index up to, but not including, another.",
        ["flatten"] = "Flattens the nested lists of a list into one list, one level or up to a given depth.",
        ["sort"] = "Returns a list sorted in ascending order.",
        ["lists.range"] = "Returns the list of ints from 0 up to, but not including, a number.",
        ["distinct"] = "Returns a list without repeated elements, in the order of the first occurrences.",

        // The set sets.
        ["sets.contains"] = "Tells whether the first list contains every element of the second list.",
        ["sets.equivalent"] = "Tells whether two lists contain the same elements, regardless of order and repetition.",
        ["sets.intersects"] = "Tells whether two lists have at least one common element.",

        // The set encoders.
        ["base64.decode"] = "Decodes a standard base64 string into bytes.",
        ["base64.encode"] = "Encodes bytes as a standard base64 string.",
        ["json.encode"] = "Encodes a value as a JSON string.",

        // The set network.
        ["ip"] = "Parses a string as an IP address, or returns the address of a CIDR.",
        ["cidr"] = "Parses a string as an IP network in CIDR notation.",
        ["isIP"] = "Tells whether a string is a valid IP address.",
        ["isCIDR"] = "Tells whether a string is a valid IP network in CIDR notation.",
        ["ip.isCanonical"] = "Tells whether a string is an IP address in its canonical text form.",
        ["family"] = "Returns the IP version of an address, 4 or 6.",
        ["isLoopback"] = "Tells whether an IP address is a loopback address.",
        ["isGlobalUnicast"] = "Tells whether an IP address is a global unicast address.",
        ["isLinkLocalMulticast"] = "Tells whether an IP address is a link-local multicast address.",
        ["isLinkLocalUnicast"] = "Tells whether an IP address is a link-local unicast address.",
        ["isUnspecified"] = "Tells whether an IP address is the unspecified address.",
        ["containsIP"] = "Tells whether an IP network contains an IP address.",
        ["containsCIDR"] = "Tells whether an IP network contains another IP network.",
        ["isMask"] = "Tells whether the address of a CIDR is a network mask.",
        ["masked"] = "Returns the network of a CIDR with the host bits cleared.",
        ["prefixLength"] = "Returns the number of bits of the prefix of a CIDR.",

        // The set optional.
        ["optional.of"] = "Creates an optional that holds a value.",
        ["optional.ofNonZeroValue"] = "Creates an optional that holds a value, or an empty optional for a zero value.",
        ["optional.none"] = "Creates an empty optional.",
        ["value"] = "Returns the value that an optional holds, or fails when it is empty.",
        ["hasValue"] = "Tells whether an optional holds a value.",
        ["or"] = "Returns the optional itself when it holds a value, otherwise another optional.",
        ["orValue"] = "Returns the value that an optional holds, otherwise a default value.",
        ["first"] = "Returns an optional with the first element of a list, empty for an empty list.",
        ["last"] = "Returns an optional with the last element of a list, empty for an empty list.",
        ["optional.unwrap"] = "Returns the values of the non-empty optionals of a list.",
        ["unwrapOpt"] = "Returns the values of the non-empty optionals of a list.",

        // The set decimal.
        ["decimal"] = "Converts an int, a uint, a double, a string or a decimal to a decimal.",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // The description of a function of the library; null for a name without one, such as an operator or an internal name.
    internal static string? Of(string function) => Table.TryGetValue(function, out string? text) ? text : null;

    // The overloads with the description of their function; the same array when no function of them has one.
    internal static ImmutableArray<OverloadBinding> Describe(ImmutableArray<OverloadBinding> overloads)
    {
        ImmutableArray<OverloadBinding>.Builder? described = null;
        for (int i = 0; i < overloads.Length; i++)
        {
            OverloadBinding binding = overloads[i];
            if (Of(binding.Function) is not string text)
            {
                continue;
            }

            if (described is null)
            {
                described = ImmutableArray.CreateBuilder<OverloadBinding>(overloads.Length);
                described.AddRange(overloads);
            }

            described[i] = binding.WithDeclaration(binding.Declaration.WithDescription(text));
        }

        return described is null ? overloads : described.MoveToImmutable();
    }
}
