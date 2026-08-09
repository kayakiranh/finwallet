using FakeCutoff.Api.Contracts;
using FakeCutoff.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<CutoffCalendarService>();

var app = builder.Build();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok", service = "FakeCutoff.Api" }));

app.MapPost("/api/v1/cutoffs/evaluate", async (
    CutoffEvaluationRequest request,
    CutoffCalendarService cutoffService,
    HttpContext httpContext) =>
{
    var fakeMode = httpContext.Request.Headers["X-Fake-Mode"].ToString();
    if (string.Equals(fakeMode, "fail", StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    if (string.Equals(fakeMode, "delay", StringComparison.OrdinalIgnoreCase))
    {
        await Task.Delay(TimeSpan.FromSeconds(2), httpContext.RequestAborted);
    }

    if (string.Equals(fakeMode, "timeout", StringComparison.OrdinalIgnoreCase))
    {
        await Task.Delay(TimeSpan.FromSeconds(30), httpContext.RequestAborted);
    }

    try
    {
        return Results.Ok(cutoffService.Evaluate(request));
    }
    catch (ArgumentException exception)
    {
        return Results.UnprocessableEntity(new
        {
            code = "CUTOFF_RULE_NOT_AVAILABLE",
            message = exception.Message
        });
    }
});

app.Run();
