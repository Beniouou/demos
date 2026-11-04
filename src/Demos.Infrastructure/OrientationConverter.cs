using System.Text.Json;
using System.Text.Json.Serialization;
using Demos.Domain.Politics;

namespace Demos.Infrastructure;

/// <summary>Un pôle idéologique s'écrit avec son code dans les fichiers JSON : « EG », « LI »…</summary>
internal sealed class OrientationConverter : JsonConverter<Orientation>
{
    public override Orientation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var code = reader.GetString() ?? throw new JsonException("Pôle sans code.");
        try
        {
            return Orientation.FromCode(code);
        }
        catch (ArgumentException e)
        {
            throw new JsonException(e.Message);
        }
    }

    public override void Write(Utf8JsonWriter writer, Orientation value, JsonSerializerOptions options) => writer.WriteStringValue(value.Code);
}
