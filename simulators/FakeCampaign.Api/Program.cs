using FakeCampaign.Api.Services;
using FinWallet.Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddFinWalletWebPlatform("FakeCampaign.Api");
builder.Services.AddControllers();
builder.Services.AddSingleton<CampaignEvaluationService>();

var app = builder.Build();

app.UseFinWalletWebPlatform();
app.MapControllers();

app.Run();
