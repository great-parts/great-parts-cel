using System.Collections;
using System.Globalization;
using System.Text;

namespace SpeedBench;

/// <summary>Canonical text of a native result: type and value, doubles in format "R".</summary>
public static class Canon
{
    public static string Format(object? value)
    {
        var sb = new StringBuilder();
        Append(sb, value);
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, object? value)
    {
        switch (value)
        {
            case null:
                sb.Append("null");
                break;
            case bool b:
                sb.Append("bool:").Append(b ? "true" : "false");
                break;
            case long or int or short or sbyte:
                sb.Append("int:").Append(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
            case ulong or uint or ushort or byte:
                sb.Append("uint:").Append(Convert.ToUInt64(value, CultureInfo.InvariantCulture));
                break;
            case double d:
                sb.Append("double:").Append(d.ToString("R", CultureInfo.InvariantCulture));
                break;
            case float f:
                sb.Append("double:").Append(((double)f).ToString("R", CultureInfo.InvariantCulture));
                break;
            case string s:
                sb.Append("string:'").Append(s).Append('\'');
                break;
            case byte[] bytes:
                sb.Append("bytes:").Append(Convert.ToHexString(bytes));
                break;
            case DateTimeOffset dto:
                sb.Append("timestamp:").Append(dto.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
                break;
            case DateTime dt:
                sb.Append("timestamp:").Append(dt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                break;
            case TimeSpan ts:
                sb.Append("duration:").Append(ts.Ticks * 100).Append("ns");
                break;
            case IDictionary dict:
            {
                var entries = new List<string>();
                foreach (DictionaryEntry e in dict)
                {
                    entries.Add(Format(e.Key) + "=>" + Format(e.Value));
                }

                entries.Sort(StringComparer.Ordinal);
                sb.Append('{').Append(string.Join(",", entries)).Append('}');
                break;
            }

            case IEnumerable seq:
            {
                sb.Append('[');
                var first = true;
                foreach (var item in seq)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Append(sb, item);
                }

                sb.Append(']');
                break;
            }

            default:
                sb.Append(value.GetType().Name).Append(':').Append(value);
                break;
        }
    }
}
