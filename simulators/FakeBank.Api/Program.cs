using FakeBank.Api.Services;
using FinWallet.Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddFinWalletWebPlatform("FakeBank.Api");
builder.Services.AddControllers();
builder.Services.AddSingleton<FakeBankProviderService>();

var app = builder.Build();

app.UseFinWalletWebPlatform();
app.MapControllers();

app.Run();
