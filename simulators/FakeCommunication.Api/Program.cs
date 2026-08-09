using System.Collections.Concurrent;
using FakeCommunication.Api.Contracts;
using FakeCommunication.Api.Models;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var messages = new ConcurrentDictionary<Guid, FakeMessageRecord>();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok", service = "FakeCommunication.Api" }));

app.MapPost("/api/v1/sms", async (SendSmsRequest request, HttpContext httpContext) =>
{
    if (string.IsNullOrWhiteSpace(request.Recipient)
        || string.IsNullOrWhiteSpace(request.MessageType)
        || string.IsNullOrWhiteSpace(request.Body)
        || string.IsNullOrWhiteSpace(request.CorrelationId))
    {
        return Results.BadRequest(new { code = "INVALID_MESSAGE_REQUEST" });
    }

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

    var messageId = Guid.NewGuid();
    var acceptedAt = DateTimeOffset.UtcNow;
    var record = new FakeMessageRecord(
        messageId,
        request.Recipient,
        request.MessageType,
        request.Body,
        request.CorrelationId,
        acceptedAt);

    messages[messageId] = record;
    return Results.Accepted($"/api/v1/dev/messages/{messageId}", new SendMessageResponse(messageId, "Accepted", acceptedAt));
});

app.MapGet("/api/v1/dev/messages/{messageId:guid}", (Guid messageId) =>
{
    return messages.TryGetValue(messageId, out var message)
        ? Results.Ok(message)
        : Results.NotFound(new { code = "MESSAGE_NOT_FOUND" });
});

app.Run();
