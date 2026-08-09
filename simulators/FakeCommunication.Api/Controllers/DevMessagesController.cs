using System.Collections.Concurrent;
using FakeCommunication.Api.Models;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FakeCommunication.Api.Controllers;

/// <summary>
/// TR: FakeCommunication simulatorının development/test amaçlı kabul edilmiş mesaj kayıtlarını sorgulama endpoint'lerini sunar.
/// EN: Exposes development/testing endpoints for inspecting accepted message records in the FakeCommunication simulator.
/// </summary>
[ApiController]
[Route("api/v1/dev/messages")]
public sealed class DevMessagesController : ControllerBase
{
    private readonly ConcurrentDictionary<Guid, FakeMessageRecord> _messages;

    /// <summary>
    /// TR: Fake provider mesaj deposu bağımlılığıyla development controller'ını oluşturur.
    /// EN: Creates the development controller with the fake-provider message-store dependency.
    /// </summary>
    /// <param name="messages">TR: Kabul edilmiş fake mesajları tutan thread-safe in-memory store. EN: Thread-safe in-memory store containing accepted fake messages.</param>
    public DevMessagesController(ConcurrentDictionary<Guid, FakeMessageRecord> messages)
    {
        _messages = messages ?? throw new ArgumentNullException(nameof(messages));
    }

    /// <summary>
    /// TR: Belirli fake provider mesaj kaydını test amacıyla döndürür.
    /// EN: Returns a specific fake-provider message record for testing purposes.
    /// </summary>
    /// <param name="messageId">TR: Sorgulanacak fake provider mesaj kimliği. EN: Fake-provider message identifier to query.</param>
    /// <returns>TR: Mesaj bulunursa başarılı, bulunamazsa MESSAGE_NOT_FOUND hata kodlu ServiceResult döndürür. EN: Returns a successful ServiceResult when found or MESSAGE_NOT_FOUND when absent.</returns>
    [HttpGet("{messageId:guid}")]
    [ProducesResponseType(typeof(ServiceResult<FakeMessageRecord>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<FakeMessageRecord>), StatusCodes.Status404NotFound)]
    public ActionResult<ServiceResult<FakeMessageRecord>> GetById(Guid messageId)
    {
        if (!_messages.TryGetValue(messageId, out var message))
        {
            return NotFound(ServiceResult<FakeMessageRecord>.Failure(
                "MESSAGE_NOT_FOUND",
                "Fake provider message was not found."));
        }

        return Ok(ServiceResult<FakeMessageRecord>.Success(message, "MESSAGE_FOUND", "Fake provider message found."));
    }
}
