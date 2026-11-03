using System.Text.Json;
using System.Text.Json.Serialization;
using Demos.Domain.Economy;

namespace Demos.Infrastructure;

/// <summary>Un besoin s'écrit par son identifiant (« Food »), comme valeur ou comme clé (« needsByRank »).</summary>
internal sealed class NeedCategoryConverter : JsonConverter<NeedCategory>
{
    public override NeedCategory Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString() ?? throw new JsonException("Besoin sans identifiant."));

    public override void Write(Utf8JsonWriter writer, NeedCategory value, JsonSerializerOptions options) => writer.WriteStringValue(value.Id);

    public override NeedCategory ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Read(ref reader, typeToConvert, options);

    public override void WriteAsPropertyName(Utf8JsonWriter writer, NeedCategory value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Id);
}
