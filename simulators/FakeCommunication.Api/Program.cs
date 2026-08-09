using System.Collections.Concurrent;
using FakeCommunication.Api.Models;
using FinWallet.Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddFinWalletWebPlatform("FakeCommunication.Api");
builder.Services.AddControllers();
builder.Services.AddSingleton<ConcurrentDictionary<Guid, FakeMessageRecord>>();

var app = builder.Build();

app.UseFinWalletWebPlatform();
app.MapControllers();

app.Run();
