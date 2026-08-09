namespace FinWallet.Shared.Contracts;

/// <summary>
/// TR: HTTP servislerinin liveness endpoint'lerinde servis adı ve sağlık durumunu taşıyan ortak response modelini temsil eder.
/// EN: Represents the shared response model carrying service name and health status for HTTP-service liveness endpoints.
/// </summary>
public sealed class HealthResponse
{
    /// <summary>
    /// TR: Sağlık response modelini oluşturur.
    /// EN: Creates the health response model.
    /// </summary>
    /// <param name="service">TR: Sağlık bilgisi döndürülen servis adı. EN: Name of the service whose health is reported.</param>
    /// <param name="status">TR: Servisin liveness durumunu ifade eden kısa değer. EN: Short value describing the service liveness state.</param>
    public HealthResponse(string service, string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        Service = service.Trim();
        Status = status.Trim();
    }

    /// <summary>
    /// TR: Sağlık response'unun ait olduğu servis adını döndürür.
    /// EN: Gets the service name to which the health response belongs.
    /// </summary>
    public string Service { get; }

    /// <summary>
    /// TR: Servisin liveness durumunu döndürür.
    /// EN: Gets the service liveness status.
    /// </summary>
    public string Status { get; }
}
