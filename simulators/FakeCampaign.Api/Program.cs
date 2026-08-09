using FakeCampaign.Api.Contracts;
using FakeCampaign.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<CampaignEvaluationService>();

var app = builder.Build();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok", service = "FakeCampaign.Api" }));

app.MapPost("/api/v1/campaigns/evaluate", async (
    CampaignEvaluationRequest request,
    CampaignEvaluationService campaignService,
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
        return Results.Ok(campaignService.Evaluate(request));
    }
    catch (ArgumentException exception)
    {
        return Results.UnprocessableEntity(new
        {
            code = "INVALID_CAMPAIGN_REQUEST",
            message = exception.Message
        });
    }
});

app.Run();
