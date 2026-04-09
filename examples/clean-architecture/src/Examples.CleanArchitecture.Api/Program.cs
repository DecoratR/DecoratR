using Examples.CleanArchitecture.Api;
using Examples.CleanArchitecture.Api.Endpoints;
using Examples.Shared;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDecoratR();
builder.Services.AddSharedInfrastructure();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapTodoEndpoints();

app.Run();