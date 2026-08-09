using FakeBank.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<FakeBankProviderService>();

var app = builder.Build();

app.MapControllers();

app.Run();
