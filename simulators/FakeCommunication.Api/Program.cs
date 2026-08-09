using System.Collections.Concurrent;
using FakeCommunication.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<ConcurrentDictionary<Guid, FakeMessageRecord>>();

var app = builder.Build();

app.MapControllers();

app.Run();
