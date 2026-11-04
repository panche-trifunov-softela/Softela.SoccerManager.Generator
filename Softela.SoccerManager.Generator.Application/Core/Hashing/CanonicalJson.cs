using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Softela.SoccerManager.Generator.Application.Core.Hashing;

/// <summary>
/// Turns a value into canonical JSON text: the same data always gives the same text, so the text can be hashed.
/// </summary>
public static class CanonicalJson
{
    private const string RoundTripFormat = "O";

    private static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>
    /// Serializes the value to canonical JSON. The rules are:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// Object keys (properties and dictionary keys) are sorted by ordinal string comparison at every level.
    /// </description>
    /// </item>
    /// <item><description>Arrays keep their order.</description></item>
    /// <item><description>There is no insignificant whitespace.</description></item>
    /// <item><description>Enums are written as their names.</description></item>
    /// <item>
    /// <description>
    /// A <see cref="DateTime"/>, as a value or as a dictionary key, must be UTC and is written in the round-trip
    /// format "O", which ends in Z.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// A <see cref="DateTimeOffset"/>, as a value or as a dictionary key, is normalised to UTC and written the same
    /// way.
    /// </description>
    /// </item>
    /// <item><description>Property names are the CLR names, with no naming policy.</description></item>
    /// <item>
    /// <description>
    /// Public properties and fields (including the items of a value tuple) are written by their declared type, so a
    /// plan should hold concrete types: an interface- or base-typed member writes only that type's members.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The canonical JSON text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the value holds a <see cref="DateTime"/> (as a value or as a dictionary key) whose kind is not UTC,
    /// or a non-finite number (NaN or Infinity).
    /// </exception>
    /// <exception cref="JsonException">
    /// Thrown when the value holds a reference cycle or is nested deeper than 64 levels.
    /// </exception>
    public static string Serialize(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        JsonNode? node;

        try
        {
            node = JsonSerializer.SerializeToNode(value, value.GetType(), Options);
        }
        catch (JsonException exception) when (exception.InnerException is ArgumentException argumentException)
        {
            // Keeps the documented ArgumentException if a later System.Text.Json wraps it.
            ExceptionDispatchInfo.Capture(argumentException).Throw();

            throw;
        }

        return Canonicalize(node)?.ToJsonString() ?? "null";
    }

    /// <summary>
    /// Builds the serializer options that every canonical serialization shares.
    /// </summary>
    /// <returns>The options, with no indentation, public fields included and the canonical converters.</returns>
    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new() { WriteIndented = false, IncludeFields = true };

        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new UtcDateTimeConverter());
        options.Converters.Add(new UtcDateTimeOffsetConverter());

        return options;
    }

    /// <summary>
    /// Rebuilds a node so that the keys of every object are in ordinal order.
    /// </summary>
    /// <param name="node">The node to rebuild; null stands for a JSON null.</param>
    /// <returns>A new node with sorted keys, or null for a JSON null.</returns>
    private static JsonNode? Canonicalize(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                JsonObject sorted = new();

                IEnumerable<KeyValuePair<string, JsonNode?>> ordered =
                    jsonObject.OrderBy(p => p.Key, StringComparer.Ordinal);

                foreach (KeyValuePair<string, JsonNode?> property in ordered)
                {
                    sorted.Add(property.Key, Canonicalize(property.Value));
                }

                return sorted;

            case JsonArray jsonArray:
                JsonArray rebuilt = new();

                foreach (JsonNode? item in jsonArray)
                {
                    rebuilt.Add(Canonicalize(item));
                }

                return rebuilt;

            default:
                return node?.DeepClone();
        }
    }

    /// <summary>
    /// Validates and formats a <see cref="DateTime"/> for both converters, as a value and as a dictionary key, so the
    /// two cannot drift.
    /// </summary>
    /// <param name="value">The date-time to format; its kind must be UTC.</param>
    /// <returns>The date-time in the round-trip format, which ends in Z.</returns>
    /// <exception cref="ArgumentException">Thrown when the kind of <paramref name="value"/> is not UTC.</exception>
    private static string FormatUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                $"A DateTime must be UTC to be serialized canonically, but its kind is {value.Kind}.",
                nameof(value));
        }

        return value.ToString(RoundTripFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writes a <see cref="DateTime"/> in the round-trip format and rejects any kind other than UTC.
    /// </summary>
    private sealed class UtcDateTimeConverter : JsonConverter<DateTime>
    {
        /// <summary>
        /// Not supported: canonical JSON is only ever written.
        /// </summary>
        /// <param name="reader">The reader, which is never used.</param>
        /// <param name="typeToConvert">The type to convert, which is never used.</param>
        /// <param name="options">The serializer options, which are never used.</param>
        /// <returns>Nothing; it always throws.</returns>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException("Canonical JSON is write-only.");
        }

        /// <summary>
        /// Writes the UTC date-time in the round-trip format.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The date-time to write.</param>
        /// <param name="options">The serializer options, which are not used.</param>
        /// <exception cref="ArgumentException">Thrown when the kind of <paramref name="value"/> is not UTC.</exception>
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(FormatUtc(value));
        }

        /// <summary>
        /// Writes the UTC date-time, used as a dictionary key, in the round-trip format.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The date-time to write as a property name.</param>
        /// <param name="options">The serializer options, which are not used.</param>
        /// <exception cref="ArgumentException">Thrown when the kind of <paramref name="value"/> is not UTC.</exception>
        public override void WriteAsPropertyName(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WritePropertyName(FormatUtc(value));
        }
    }

    /// <summary>
    /// Writes a <see cref="DateTimeOffset"/> normalised to UTC in the round-trip format.
    /// </summary>
    private sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        /// <summary>
        /// Not supported: canonical JSON is only ever written.
        /// </summary>
        /// <param name="reader">The reader, which is never used.</param>
        /// <param name="typeToConvert">The type to convert, which is never used.</param>
        /// <param name="options">The serializer options, which are never used.</param>
        /// <returns>Nothing; it always throws.</returns>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public override DateTimeOffset Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            throw new NotSupportedException("Canonical JSON is write-only.");
        }

        /// <summary>
        /// Writes the instant in UTC in the round-trip format.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The date-time offset to write.</param>
        /// <param name="options">The serializer options, which are not used.</param>
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(FormatUtc(value.UtcDateTime));
        }

        /// <summary>
        /// Writes the instant, used as a dictionary key, in UTC in the round-trip format.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The date-time offset to write as a property name.</param>
        /// <param name="options">The serializer options, which are not used.</param>
        public override void WriteAsPropertyName(
            Utf8JsonWriter writer,
            DateTimeOffset value,
            JsonSerializerOptions options)
        {
            writer.WritePropertyName(FormatUtc(value.UtcDateTime));
        }
    }
}
