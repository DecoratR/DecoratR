using System.Net.ServerSentEvents;
using System.Text.Json.Serialization;
using DecoratR.Sample.Presentation.Endpoints;

namespace DecoratR.Sample.Presentation;

internal sealed record Response(string Message);

[JsonSerializable(typeof(GreetRequest))]
[JsonSerializable(typeof(Response))]
[JsonSerializable(typeof(SseItem<string>))]
internal sealed partial class DefaultSerializerContext : JsonSerializerContext;