using EncuestasCentral.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EncuestasCentral.Api;

/// <summary>Aloja la API REST local (Kestrel) dentro del proceso WPF.</summary>
public class ApiHost
{
    public const string Version = "0.1";

    private readonly ServerState _state;
    private WebApplication? _app;

    public ApiHost(ServerState state) => _state = state;

    public bool Running => _app != null;

    public async Task StartAsync(int port)
    {
        if (_app != null) return;

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        // Permitir payloads grandes: las respuestas embeben imágenes como Base64
        // y el lote puede superar el límite por defecto de Kestrel (HTTP 413 Payload Too Large).
        builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 200_000_000L); // 200 MB
        builder.Services.AddSingleton(_state);
        builder.Services.AddDbContext<AppDbContext>();
        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            o.SerializerOptions.PropertyNameCaseInsensitive = true;
        });

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.Database.EnsureCreated();
            ctx.EnsureAuxSchema();
        }

        Endpoints.Map(app);
        _app = app;
        await app.RunAsync();
    }

    public async Task StopAsync()
    {
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }
    }
}
