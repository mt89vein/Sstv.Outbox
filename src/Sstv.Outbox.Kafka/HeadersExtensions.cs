using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Sstv.Outbox.Kafka;

internal static class HeadersExtensions
{
    public static Dictionary<string, string> GetHeaders(this ActivityContext context)
    {
        Dictionary<string, string> dict = new();

        Propagators.DefaultTextMapPropagator.Inject(
            new PropagationContext(context, Baggage.Current),
            dict,
            (dictionary, s, arg3) => dictionary[s] = arg3);

        return dict;
    }
}
