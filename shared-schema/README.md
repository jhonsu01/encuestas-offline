# Shared Schema — Contrato de datos

Este directorio define el **contrato común** entre la app Android, la app Windows y la API local.
Ambos lados (Kotlin y C#) serializan/deserializan estos modelos.

## Formulario de encuesta (`survey-example.json`)

Un formulario es un JSON con una lista de `questions`. Cada pregunta tiene un `type`:

| type              | Renderiza                         | Validación                     |
| ----------------- | --------------------------------- | ------------------------------ |
| `text`            | Campo de texto                    | `required`                     |
| `single_choice`   | Radio buttons                     | `required`, valor ∈ `options`  |
| `multiple_choice` | Checkboxes                        | valores ⊆ `options`            |
| `number`          | Campo numérico                    | `required`, `min`, `max`       |
| `date`            | Selector de fecha (ISO-8601)      | `required`                     |
| `image`           | Captura CameraX (obligatoria)     | `required` ⇒ imagen no nula    |
| `gps`             | Coordenadas FusedLocation         | `required` ⇒ lat/lon no nulos  |

## Ventana horaria (`schedule`)

`startTime`/`endTime` en formato `HH:mm`. Fuera de la ventana, la app Android
bloquea la captura (ver `HorarioValidator`).

## Lote de sincronización (contrato REST)

`POST /sync` recibe un `SyncBatch`:

```json
{
  "pin": "483920",
  "deviceId": "android-abc123",
  "surveyorDocument": "CC-1020304050",
  "responses": [
    {
      "surveyId": "survey-demo-001",
      "surveyorId": "CC-1020304050",
      "timestamp": "2026-07-04T14:03:22Z",
      "answers": { "q1": "Ana Pérez", "q2": "3", "q4": "4" },
      "latitude": 4.7110,
      "longitude": -74.0721,
      "imageBase64": "<...>",
      "signature": "9f2c...<sha256>"
    }
  ]
}
```

## Firma de integridad (SHA256)

Cada respuesta se firma para detectar manipulación/duplicados:

```
signature = SHA256( surveyId + surveyorId + timestamp + serialize(answers) )
```

`serialize(answers)` = pares `clave=valor` ordenados por clave y unidos por `;`.
La implementación es **idéntica** en Kotlin (`FirmaEncuesta`) y C# (`SignatureService`)
para que el hash coincida a ambos lados.
