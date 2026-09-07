using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace api.Json
{
    /// Accepts a JSON number, a numeric string, or null - legacy Arduino-String-based firmware (pre roadmap #326) still sends measurement fields as strings.
    public sealed class LenientDoubleConverter : JsonConverter<double?>
    {
        public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.GetDouble();
            }
            if (reader.TokenType == JsonTokenType.String
                && double.TryParse(reader.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            {
                return d;
            }
            return null;
        }

        public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
        {
            if (value is double d) writer.WriteNumberValue(d);
            else writer.WriteNullValue();
        }
    }

    /// Same tolerance as LenientDoubleConverter, for int? fields - a JSON number with a fractional part truncates instead of failing, same as the manual parsing it replaces.
    public sealed class LenientIntConverter : JsonConverter<int?>
    {
        public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.TryGetInt32(out int i) ? i : (int)reader.GetDouble();
            }
            if (reader.TokenType == JsonTokenType.String)
            {
                string? s = reader.GetString();
                if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var si)) return si;
                if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var sd)) return (int)sd;
            }
            return null;
        }

        public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
        {
            if (value is int i) writer.WriteNumberValue(i);
            else writer.WriteNullValue();
        }
    }
}
