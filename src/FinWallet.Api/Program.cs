var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok", service = "FinWallet.Api" }));

app.Run();
