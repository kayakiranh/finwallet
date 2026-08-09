using FakeFraud.Api.Services;
using FinWallet.Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddFinWalletWebPlatform("FakeFraud.Api");
builder.Services.AddControllers();
builder.Services.AddSingleton<FraudEvaluationService>();

var app = builder.Build();

app.UseFinWalletWebPlatform();
app.MapControllers();

app.Run();
