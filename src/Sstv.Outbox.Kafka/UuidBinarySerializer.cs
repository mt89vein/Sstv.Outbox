using System.Text.Json;
using Confluent.Kafka;

namespace Sstv.Outbox.Kafka;

/// <summary>
/// UUID binary serializer.
/// </summary>
public sealed class UuidBinarySerializer : ISerializer<Guid>, IDeserializer<Guid>
{
    /// <inheritdoc />
    public byte[] Serialize(Guid data, SerializationContext context)
    {
        return data.ToByteArray(bigEndian: true);
    }

    /// <inheritdoc />
    public Guid Deserialize(
        ReadOnlySpan<byte> data,
        bool isNull,
        SerializationContext context
    )
    {
        return new Guid(data, bigEndian: true);
    }
}

/// <summary>
/// Json serializer.
/// </summary>
public sealed class SystemTextJsonSerializer<T> : ISerializer<T>, IDeserializer<T>
    where T : class
{
    /// <summary>
    /// Settings.
    /// </summary>
    private readonly JsonSerializerOptions? _jsonSerializerOptions;

    /// <summary>
    /// Creates new instance of <see cref="SystemTextJsonSerializer{T}"/>.
    /// </summary>
    /// <param name="jsonSerializerOptions">Settings.</param>
    public SystemTextJsonSerializer(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        _jsonSerializerOptions = jsonSerializerOptions;
    }

    /// <inheritdoc />
    public byte[] Serialize(T data, SerializationContext context)
    {
        return JsonSerializer.SerializeToUtf8Bytes(data, _jsonSerializerOptions);
    }

    /// <inheritdoc />
    public T Deserialize(
        ReadOnlySpan<byte> data,
        bool isNull,
        SerializationContext context
    )
    {
        return isNull
            ? default!
            : JsonSerializer.Deserialize<T>(data, _jsonSerializerOptions)!;
    }
}
