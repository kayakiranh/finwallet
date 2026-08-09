using FakeFraud.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<FraudEvaluationService>();

var app = builder.Build();

app.MapControllers();

app.Run();
