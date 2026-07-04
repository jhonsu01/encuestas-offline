# API Local (ASP.NET Core)

La API REST local **se ejecuta embebida dentro de la app Windows** (`EncuestasCentral`)
usando Kestrel en un hilo en segundo plano — así el operador solo instala **un** programa
(el MSI) y obtiene UI + servidor. El código vive en:

```
windows-app/src/EncuestasCentral/Api/ApiHost.cs      # arranque de Kestrel + endpoints
windows-app/src/EncuestasCentral/Api/Endpoints.cs    # /health /surveys /auth /sync
```

Este directorio documenta el **contrato** para consumidores externos.

## Endpoints

### `GET /health`
```json
{ "status": "ok", "service": "ENCUESTAS", "version": "0.1", "time": "2026-07-04T14:00:00Z" }
```

### `GET /surveys`
Devuelve la lista de encuestas publicadas (array de formularios del `shared-schema`).

### `POST /auth`
Request: `{ "pin": "483920", "deviceId": "android-abc123" }`
Response: `{ "token": "<jwt-o-guid>", "expiresIn": 3600 }`

### `POST /sync`
Request: `SyncBatch` (ver `shared-schema/README.md`).
Response: `{ "accepted": 12, "duplicates": 1, "rejected": 0, "batchId": "..." }`

## Puerto

Por defecto **5000** (configurable en `appsettings.json` de la app Windows).
Escucha en `0.0.0.0` para ser accesible desde la LAN.
