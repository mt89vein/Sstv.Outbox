using Confluent.Kafka;
using Sstv.Outbox.Kafka;

namespace Sstv.Outbox.Sample.Extensions;

internal static class OutboxItemHandlerBuilderExtensions
{
    public static OutboxItemHandlerBuilder<TOutboxItem> ProduceToKafka<TOutboxItem>(
        this OutboxItemHandlerBuilder<TOutboxItem> builder
    ) where TOutboxItem : class, IKafkaOutboxItem, new()
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithKafkaProducer<OutboxKafkaHandler<TOutboxItem>, TOutboxItem, Guid, NotificationMessage>(
            new KafkaTopicConfig<Guid, NotificationMessage>
            {
                DefaultTopicName = "notification-messages",
                KeyDeserializer = new UuidBinarySerializer(),
                KeySerializer = new UuidBinarySerializer(),
                ValueDeserializer = new SystemTextJsonSerializer<NotificationMessage>(),
                ValueSerializer = new SystemTextJsonSerializer<NotificationMessage>(),
                Producer = new ProducerBuilder<byte[]?, byte[]?>(new ProducerConfig
                {
                    SecurityProtocol = SecurityProtocol.Plaintext,
                    BootstrapServers = "localhost:9092"
                }).Build()
            });
    }
}


