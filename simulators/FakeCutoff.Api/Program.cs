using FakeCutoff.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<CutoffCalendarService>();

var app = builder.Build();

app.MapControllers();

app.Run();
