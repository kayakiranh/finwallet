using FinWallet.Api.Contracts.Wallets;
using FinWallet.Application.Wallets;
using FinWallet.Domain.Shared;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinWallet.Api.Controllers;

/// <summary>
/// TR: Authenticated customer'ın currency wallet oluşturma ve listeleme use-case'lerini controller tabanlı Web API üzerinden sunar.
/// EN: Exposes currency-wallet creation and listing use cases for authenticated customers through controller-based Web API.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/wallets")]
public sealed class WalletsController : ControllerBase
{
    private readonly CreateWalletHandler _createWalletHandler;
    private readonly ListWalletsHandler _listWalletsHandler;

    /// <summary>TR: Wallet use-case handler bağımlılıklarıyla controller'ı oluşturur. EN: Creates the controller with its wallet use-case handler dependencies.</summary>
    /// <param name="createWalletHandler">TR: Idempotent wallet create handler'ı. EN: Idempotent wallet-create handler.</param>
    /// <param name="listWalletsHandler">TR: Customer wallet listeleme handler'ı. EN: Customer-wallet listing handler.</param>
    public WalletsController(CreateWalletHandler createWalletHandler, ListWalletsHandler listWalletsHandler)
    {
        _createWalletHandler = createWalletHandler ?? throw new ArgumentNullException(nameof(createWalletHandler));
        _listWalletsHandler = listWalletsHandler ?? throw new ArgumentNullException(nameof(listWalletsHandler));
    }

    /// <summary>
    /// TR: JWT subject customer için TRY/USD/EUR wallet oluşturur; aynı currency zaten varsa mevcut wallet'ı idempotent biçimde döndürür.
    /// EN: Creates a TRY/USD/EUR wallet for the JWT-subject customer or idempotently returns the existing wallet for the same currency.
    /// </summary>
    /// <param name="request">TR: Currency kodunu taşıyan create request. EN: Create request carrying the currency code.</param>
    /// <param name="cancellationToken">TR: SQL işlemlerine yayılan request iptal sinyali. EN: Request cancellation signal propagated to SQL operations.</param>
    /// <returns>TR: Yeni create için 201, mevcut wallet için 200 ve ServiceResult body döndürür. EN: Returns 201 for a new wallet or 200 for an existing wallet, with a ServiceResult body.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ServiceResult<WalletResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ServiceResult<WalletResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<WalletResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ServiceResult<WalletResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServiceResult<WalletResponse>>> CreateAsync(
        [FromBody] CreateWalletRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedCustomerId(out var customerId))
        {
            return Unauthorized(ServiceResult<WalletResponse>.Failure(
                "INVALID_ACCESS_TOKEN",
                "The access token subject is invalid."));
        }

        if (!TryParseCurrency(request.Currency, out var currency))
        {
            return BadRequest(ServiceResult<WalletResponse>.Failure(
                "UNSUPPORTED_CURRENCY",
                "Currency must be TRY, USD or EUR."));
        }

        var result = await _createWalletHandler.HandleAsync(
            new CreateWalletCommand(customerId, currency),
            cancellationToken);
        var response = new WalletResponse(result.Wallet);
        var serviceResult = ServiceResult<WalletResponse>.Success(
            response,
            result.WasCreated ? "WALLET_CREATED" : "WALLET_EXISTS",
            result.WasCreated ? "Wallet created successfully." : "Wallet already exists for this currency.");

        return result.WasCreated
            ? StatusCode(StatusCodes.Status201Created, serviceResult)
            : Ok(serviceResult);
    }

    /// <summary>TR: JWT subject customer'a ait tüm wallet'ları listeler. EN: Lists all wallets owned by the JWT-subject customer.</summary>
    /// <param name="cancellationToken">TR: SQL sorgusuna yayılan request iptal sinyali. EN: Request cancellation signal propagated to the SQL query.</param>
    /// <returns>TR: Currency sırasındaki wallet koleksiyonunu ServiceResult içinde döndürür. EN: Returns wallets ordered by currency inside ServiceResult.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ServiceResult<IReadOnlyCollection<WalletResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ServiceResult<IReadOnlyCollection<WalletResponse>>>> ListAsync(CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedCustomerId(out var customerId))
        {
            return Unauthorized(ServiceResult<IReadOnlyCollection<WalletResponse>>.Failure(
                "INVALID_ACCESS_TOKEN",
                "The access token subject is invalid."));
        }

        var results = await _listWalletsHandler.HandleAsync(customerId, cancellationToken);
        IReadOnlyCollection<WalletResponse> response = results.Select(static result => new WalletResponse(result)).ToArray();
        return Ok(ServiceResult<IReadOnlyCollection<WalletResponse>>.Success(
            response,
            "WALLETS_RETRIEVED",
            "Wallets retrieved successfully."));
    }

    /// <summary>TR: Validated JWT içindeki `sub` claim'ini FinWallet customer GUID değerine dönüştürür. EN: Converts the `sub` claim from the validated JWT into a FinWallet customer GUID.</summary>
    /// <param name="customerId">TR: Parse başarılıysa authenticated customer kimliğini alır. EN: Receives authenticated customer identifier when parsing succeeds.</param>
    /// <returns>TR: `sub` claim'i geçerli FinWallet GUID ise true döndürür. EN: Returns true when the `sub` claim is a valid FinWallet GUID.</returns>
    private bool TryGetAuthenticatedCustomerId(out Guid customerId)
    {
        var subject = User.FindFirst("sub")?.Value;
        return Guid.TryParseExact(subject, "N", out customerId);
    }

    /// <summary>TR: API currency metnini desteklenen CurrencyCode enumuna dönüştürür. EN: Converts API currency text into a supported CurrencyCode enum.</summary>
    /// <param name="value">TR: İstemciden gelen currency metni. EN: Currency text supplied by the client.</param>
    /// <param name="currency">TR: Parse başarılıysa desteklenen currency değerini alır. EN: Receives supported currency when parsing succeeds.</param>
    /// <returns>TR: Değer TRY/USD/EUR ise true döndürür. EN: Returns true when the value is TRY/USD/EUR.</returns>
    private static bool TryParseCurrency(string value, out CurrencyCode currency)
    {
        return Enum.TryParse(value, ignoreCase: true, out currency) && Enum.IsDefined(currency);
    }
}
