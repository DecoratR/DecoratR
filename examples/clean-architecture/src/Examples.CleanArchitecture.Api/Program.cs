using Examples.CleanArchitecture.Api;
using Examples.CleanArchitecture.Api.Endpoints;
using Examples.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDecoratR();
builder.Services.AddSharedInfrastructure();

var app = builder.Build();

app.MapTodoEndpoints();

app.Run();
