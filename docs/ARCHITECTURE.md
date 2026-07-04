# Arquitectura técnica

## Visión general

```
┌───────────────────────────┐         WiFi / LAN          ┌───────────────────────────┐
│        ANDROID (campo)     │  ◀── mDNS / UDP 8888 ──▶    │      WINDOWS (central)     │
│                            │                             │                            │
│  Compose UI                │        REST (HTTP)          │  WPF UI (editor+dashboard) │
│  ├─ Registro encuestador   │  ── GET  /health ──▶        │  ├─ Editor de encuestas    │
│  ├─ Motor de formularios   │  ── GET  /surveys ──▶       │  ├─ Gestión encuestadores  │
│  ├─ Captura offline (Room) │  ── POST /auth  ──▶         │  ├─ Discovery service      │
│  ├─ Cámara / GPS           │  ── POST /sync  ──▶         │  └─ Dashboard              │
│  └─ Sync por lotes         │                             │  ASP.NET Core (Kestrel)    │
│                            │                             │  ├─ Endpoints REST         │
│  SQLite (Room)             │                             │  └─ SQLite (EF Core)       │
└───────────────────────────┘                             └───────────────────────────┘
```

## Capas (Clean Architecture)

### Android
- **domain**: modelos (`Survey`, `Question`, `SurveyResponse`, `Surveyor`) y lógica pura
  (`FirmaEncuesta`, `HorarioValidator`, `FormValidator`). Sin dependencias de Android.
- **data**: Room (`AppDatabase`, DAOs, entidades) + `SyncClient` (Retrofit/OkHttp) +
  `DiscoveryClient` (mDNS/UDP).
- **ui**: pantallas Compose + `ViewModel`s.

### Windows
- **Domain**: modelos y `SignatureService`, `BatchValidator`.
- **Data**: `AppDbContext` (EF Core + SQLite), repositorios.
- **Api**: endpoints minimal API montados en el mismo proceso WPF (Kestrel en background).
- **Ui**: vistas WPF (MVVM ligero).

## Contrato REST (API local)

| Método | Ruta        | Descripción                                              |
| ------ | ----------- | -------------------------------------------------------- |
| GET    | `/health`   | Ping de disponibilidad (`{ "status": "ok", ... }`)       |
| GET    | `/surveys`  | Lista de encuestas publicadas (JSON del `shared-schema`) |
| POST   | `/auth`     | Valida PIN de lote → devuelve token de sesión            |
| POST   | `/sync`     | Recibe `SyncBatch`, valida y persiste                    |

## Descubrimiento en red

1. **mDNS (Zeroconf)** — la central publica `_encuestas._tcp.local` en el puerto 5000.
   Android resuelve el servicio con `NsdManager`.
2. **UDP Broadcast (fallback)** — la central emite cada 3 s en el puerto **8888**:
   ```json
   { "type": "DISCOVERY", "service": "ENCUESTAS", "ip": "AUTO", "port": 5000 }
   ```
   Android escucha broadcasts y extrae la IP del emisor.
3. **Escaneo /24 (último recurso)** — Android recorre el rango local y prueba `/health`.

## Firma de integridad

```
signature = SHA256( surveyId + surveyorId + timestamp + serialize(answers) )
serialize(answers) = claves ordenadas, "clave=valor" unidas por ";"
```

Implementada de forma **idéntica** en `FirmaEncuesta.kt` y `SignatureService.cs`.

## Sincronización

- Solo **lotes completos** (transaccional): si una respuesta falla la validación,
  se rechaza el lote entero.
- **Idempotencia**: el servidor descarta respuestas cuya `signature` ya existe.
- **Reintentos**: el cliente Android reintenta con backoff ante error de red.

## Versionado y releases

`VERSION` (raíz) fija el `major.minor`. El patch lo aporta el número de build de CI:
`vMAJOR.MINOR.<run_number>`. Cada build publica una release nueva y **elimina las
anteriores**, dejando solo la última con los artefactos APK + MSI.
