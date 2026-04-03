using System.Text.Json.Serialization;
using DecoratR.Sample.Presentation.Endpoints;

namespace DecoratR.Sample.Presentation;

internal sealed record Response(string Message);

[JsonSerializable(typeof(GreetRequest))]
[JsonSerializable(typeof(Response))]
internal sealed partial class DefaultSerializerContext : JsonSerializerContext;