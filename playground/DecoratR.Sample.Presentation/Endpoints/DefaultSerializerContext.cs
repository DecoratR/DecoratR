using System.Text.Json.Serialization;

namespace DecoratR.Sample.Presentation.Endpoints;

internal sealed record Response(string Message);

[JsonSerializable(typeof(GreetRequest))]
[JsonSerializable(typeof(Response))]
internal sealed partial class DefaultSerializerContext : JsonSerializerContext;