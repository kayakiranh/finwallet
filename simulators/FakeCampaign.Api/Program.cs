using FakeCampaign.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<CampaignEvaluationService>();

var app = builder.Build();

app.MapControllers();

app.Run();
