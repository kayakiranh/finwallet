namespace FinWallet.Shared.Contracts;

/// <summary>
/// TR: Tüm FinWallet ve fake provider HTTP API'lerinde başarı ve hata cevaplarını tek, tip güvenli ve kararlı bir envelope altında standartlaştırır.
/// EN: Standardizes success and failure responses across all FinWallet and fake-provider HTTP APIs under one type-safe and stable envelope.
/// </summary>
/// <typeparam name="T">
/// TR: Başarılı response içinde taşınan API veri tipidir.
/// EN: API data type carried by a successful response.
/// </typeparam>
public sealed class ServiceResult<T>
{
    /// <summary>
    /// TR: ServiceResult nesnesini yalnızca factory metotları üzerinden oluşturmak için kullanılan özel kurucudur.
    /// EN: Private constructor used to create ServiceResult instances only through factory methods.
    /// </summary>
    /// <param name="isSuccess">TR: İşlemin başarılı olup olmadığını belirtir. EN: Indicates whether the operation succeeded.</param>
    /// <param name="code">TR: Sonucun kararlı makine kodudur. EN: Stable machine-readable result code.</param>
    /// <param name="message">TR: İstemciye gösterilebilecek güvenli açıklamadır. EN: Safe description that may be shown to the client.</param>
    /// <param name="data">TR: Başarılı sonuç verisidir; hata durumunda null olabilir. EN: Successful result data; may be null on failure.</param>
    /// <param name="errors">TR: Detay hata listesidir. EN: Detailed error collection.</param>
    private ServiceResult(
        bool isSuccess,
        string code,
        string message,
        T? data,
        IReadOnlyCollection<ServiceError> errors)
    {
        IsSuccess = isSuccess;
        Code = code;
        Message = message;
        Data = data;
        Errors = errors;
    }

    /// <summary>
    /// TR: İşlemin başarılı tamamlanıp tamamlanmadığını döndürür.
    /// EN: Gets whether the operation completed successfully.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// TR: İstemcinin programatik olarak değerlendirebileceği kararlı sonuç kodunu döndürür.
    /// EN: Gets the stable result code that clients can evaluate programmatically.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// TR: Hassas iç detay içermeyen güvenli sonuç açıklamasını döndürür.
    /// EN: Gets the safe result description that does not expose sensitive internal details.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// TR: Başarılı işlem sonucundaki tip güvenli veriyi döndürür; hata durumunda null olabilir.
    /// EN: Gets the type-safe data returned by a successful operation; it may be null on failure.
    /// </summary>
    public T? Data { get; }

    /// <summary>
    /// TR: Alan veya business-rule seviyesindeki detay hata listesini döndürür; hata yoksa boş koleksiyondur.
    /// EN: Gets field-level or business-rule error details; the collection is empty when no errors exist.
    /// </summary>
    public IReadOnlyCollection<ServiceError> Errors { get; }

    /// <summary>
    /// TR: Verili başarılı API sonucunu oluşturur.
    /// EN: Creates a successful API result containing data.
    /// </summary>
    /// <param name="data">TR: İstemciye döndürülecek tip güvenli veri. EN: Type-safe data returned to the client.</param>
    /// <param name="code">TR: Başarılı sonucun kararlı kodu. EN: Stable success result code.</param>
    /// <param name="message">TR: Başarılı sonucun güvenli açıklaması. EN: Safe success result description.</param>
    /// <returns>TR: Başarılı ServiceResult döndürür. EN: Returns a successful ServiceResult.</returns>
    public static ServiceResult<T> Success(T? data, string code = "SUCCESS", string message = "Operation completed successfully.")
    {
        ValidateCodeAndMessage(code, message);
        return new ServiceResult<T>(true, code.Trim(), message.Trim(), data, Array.Empty<ServiceError>());
    }

    /// <summary>
    /// TR: Tek ana hata kodu ve isteğe bağlı detay hatalarla başarısız API sonucunu oluşturur.
    /// EN: Creates a failed API result with one primary error code and optional detailed errors.
    /// </summary>
    /// <param name="code">TR: Başarısız sonucun kararlı hata kodu. EN: Stable failure result code.</param>
    /// <param name="message">TR: İstemciye güvenli hata açıklaması. EN: Client-safe failure description.</param>
    /// <param name="errors">TR: İsteğe bağlı detay hata koleksiyonu. EN: Optional detailed error collection.</param>
    /// <returns>TR: Başarısız ServiceResult döndürür. EN: Returns a failed ServiceResult.</returns>
    public static ServiceResult<T> Failure(
        string code,
        string message,
        IReadOnlyCollection<ServiceError>? errors = null)
    {
        ValidateCodeAndMessage(code, message);
        return new ServiceResult<T>(false, code.Trim(), message.Trim(), default, errors ?? Array.Empty<ServiceError>());
    }

    /// <summary>
    /// TR: Factory metotlarında sonuç kodu ve açıklamanın boş olmamasını ortak biçimde doğrular.
    /// EN: Centrally validates that result codes and descriptions are not empty in factory methods.
    /// </summary>
    /// <param name="code">TR: Doğrulanacak sonuç kodu. EN: Result code to validate.</param>
    /// <param name="message">TR: Doğrulanacak güvenli açıklama. EN: Safe description to validate.</param>
    private static void ValidateCodeAndMessage(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
    }
}
