# Network Discovery — Protocolo de descubrimiento

Módulo compartido (documentación + referencia de implementación) para que Android
encuentre automáticamente la central Windows en la red local.

## 1. mDNS / Zeroconf (principal)

- **Servicio:** `_encuestas._tcp.local`
- **Puerto:** `5000`
- **Publicación (Windows):** `Makaretu.Dns` / `Zeroconf` → ver
  `windows-app/src/EncuestasCentral/Discovery/DiscoveryService.cs`
- **Resolución (Android):** `android.net.nsd.NsdManager` → ver
  `android-app/app/src/main/java/com/encuestas/offline/data/discovery/DiscoveryClient.kt`

## 2. UDP Broadcast (fallback)

- **Puerto:** `8888`
- **Periodicidad:** cada 3 s desde la central.
- **Mensaje:**

```json
{
  "type": "DISCOVERY",
  "service": "ENCUESTAS",
  "ip": "AUTO",
  "port": 5000
}
```

`"ip": "AUTO"` indica que el receptor debe usar la IP de origen del datagrama.

## 3. Escaneo IP /24 (último recurso)

Si mDNS y UDP fallan, Android recorre el rango `x.y.z.1..254` de su propia subred
y prueba `GET /health` en el puerto 5000. La primera IP que responde `status: ok`
se toma como servidor.

## Orden de resolución

```
mDNS ──(timeout 4s)──▶ UDP broadcast ──(timeout 5s)──▶ escaneo /24 ──▶ manual (IP a mano)
```
