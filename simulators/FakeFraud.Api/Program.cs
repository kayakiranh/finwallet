var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok", service = "FakeFraud.Api" }));

app.Run();
