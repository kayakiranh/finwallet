var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok", service = "FakeCutoff.Api" }));

app.Run();
