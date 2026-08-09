using FinWallet.Application.Banking;
using FinWallet.Application.Wallets;
using FinWallet.Domain.Wallets;
using Moq;
using Xunit;

namespace FinWallet.Application.Tests.Banking;

/// <summary>
/// TR: OpenBankAccountHandler orchestration davranışlarını dış banka ve persistence bağımlılıklarını mock'layarak doğrular.
/// EN: Verifies OpenBankAccountHandler orchestration behavior by mocking external-bank and persistence dependencies.
/// </summary>
public sealed class OpenBankAccountHandlerTests
{
    /// <summary>
    /// TR: Owned wallet bulunamadığında provider çağrısının hiç başlamadığını doğrular.
    /// EN: Verifies that the external provider is never called when the owned wallet cannot be found.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenOwnedWalletDoesNotExist_DoesNotCallBankProvider()
    {
        var customerId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var walletStore = new Mock<IWalletStore>(MockBehavior.Strict);
        var bankAccountStore = new Mock<IBankAccountStore>(MockBehavior.Strict);
        var bankProvider = new Mock<IBankProvider>(MockBehavior.Strict);

        walletStore
            .Setup(store => store.FindOwnedAsync(walletId, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Wallet?)null);

        var handler = new OpenBankAccountHandler(
            walletStore.Object,
            bankAccountStore.Object,
            bankProvider.Object,
            TimeProvider.System);

        var command = new OpenBankAccountCommand(customerId, walletId, "test-correlation");

        await Assert.ThrowsAsync<BankAccountWalletNotFoundException>(
            () => handler.HandleAsync(command, CancellationToken.None));

        walletStore.VerifyAll();
        bankAccountStore.VerifyNoOtherCalls();
        bankProvider.VerifyNoOtherCalls();
    }
}
