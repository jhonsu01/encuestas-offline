using Encuestas.Core;
using EncuestasCentral.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EncuestasCentral.Api;

public static class Endpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/health", (ServerState state) => new HealthResponse
        {
            Status = "ok",
            Service = "ENCUESTAS",
            Version = ApiHost.Version,
            Time = DateTime.UtcNow.ToString("o")
        });

        app.MapGet("/surveys", (ServerState state) => state.Surveys);

        app.MapPost("/auth", (AuthRequest req) =>
        {
            var valid = req.Pin.Length is >= 6 and <= 8 && req.Pin.All(char.IsDigit);
            return valid
                ? Results.Ok(new AuthResponse { Token = Guid.NewGuid().ToString("N"), ExpiresIn = 3600 })
                : Results.BadRequest(new { error = "PIN inválido" });
        });

        app.MapPost("/sync", async (SyncBatch batch, AppDbContext db, ServerState state) =>
        {
            // Lote completo: si alguna firma es inválida, se rechaza TODO el lote.
            if (!BatchValidator.BatchIsValid(batch, out var invalid))
            {
                state.Log($"Lote RECHAZADO de {batch.SurveyorDocument}: {invalid} firma(s) inválida(s)");
                return Results.Ok(new SyncResult
                {
                    Accepted = 0,
                    Duplicates = 0,
                    Rejected = batch.Responses.Count,
                    BatchId = null
                });
            }

            var batchId = Guid.NewGuid().ToString("N");
            var now = DateTime.UtcNow.ToString("o");
            int accepted = 0, duplicates = 0;

            // Lista blanca de dispositivos.
            if (!await db.Devices.AnyAsync(d => d.DeviceId == batch.DeviceId))
            {
                db.Devices.Add(new DeviceRow
                {
                    DeviceId = batch.DeviceId,
                    SurveyorId = batch.SurveyorDocument,
                    FirstSeen = now,
                    Allowed = true
                });
            }

            foreach (var r in batch.Responses)
            {
                if (await db.Responses.AnyAsync(x => x.Signature == r.Signature))
                {
                    duplicates++;
                    continue;
                }
                db.Responses.Add(new ResponseRow
                {
                    Signature = r.Signature,
                    SurveyId = r.SurveyId,
                    SurveyorId = r.SurveyorId,
                    Timestamp = r.Timestamp,
                    AnswersJson = JsonSerializer.Serialize(r.Answers),
                    Latitude = r.Latitude,
                    Longitude = r.Longitude,
                    ImageBase64 = r.ImageBase64,
                    BatchPin = batch.Pin,
                    BatchId = batchId,
                    ReceivedAt = now
                });
                accepted++;
            }

            // Upsert encuestador.
            var surveyor = await db.Surveyors.FindAsync(batch.SurveyorDocument);
            if (surveyor == null)
            {
                db.Surveyors.Add(new SurveyorRow
                {
                    Id = batch.SurveyorDocument,
                    FirstSeen = now,
                    ResponseCount = accepted
                });
            }
            else
            {
                surveyor.ResponseCount += accepted;
            }

            db.Batches.Add(new BatchRow
            {
                BatchId = batchId,
                Pin = batch.Pin,
                DeviceId = batch.DeviceId,
                SurveyorId = batch.SurveyorDocument,
                Accepted = accepted,
                Duplicates = duplicates,
                Rejected = 0,
                ReceivedAt = now
            });

            db.Logs.Add(new LogRow
            {
                Time = now,
                Message = $"Lote {batchId[..8]} de {batch.SurveyorDocument}: " +
                          $"{accepted} aceptadas, {duplicates} duplicadas (PIN {batch.Pin})"
            });

            await db.SaveChangesAsync();
            state.Log($"Lote recibido de {batch.SurveyorDocument}: +{accepted} (dup {duplicates})");

            return Results.Ok(new SyncResult
            {
                Accepted = accepted,
                Duplicates = duplicates,
                Rejected = 0,
                BatchId = batchId
            });
        });
    }
}
