using FakeCutoff.Api.Services;
using FinWallet.Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddFinWalletWebPlatform("FakeCutoff.Api");
builder.Services.AddControllers();
builder.Services.AddSingleton<CutoffCalendarService>();

var app = builder.Build();

app.UseFinWalletWebPlatform();
app.MapControllers();

app.Run();
