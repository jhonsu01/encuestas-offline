# Network Discovery — Protocolo de descubrimiento (UDP, puerto 8888)

Descubrimiento **bidireccional** por UDP en la LAN. Diseñado para ser robusto en PCs con
varias tarjetas de red (WiFi + Ethernet + adaptadores virtuales) y con el firewall abierto
por el instalador MSI.

## 1. Activo (principal): petición/respuesta

El dispositivo Android **pregunta** y el servidor **responde unicast** (lo más fiable):

```
Android ──(broadcast :8888)──▶  { "type":"DISCOVER", "role":"device",
                                  "deviceId":"...", "name":"...", "surveyorId":"..." }
Servidor ──(unicast al origen)─▶ { "type":"SERVER", "service":"ENCUESTAS", "port":5000 }
```

- Android envía el `DISCOVER` a **todas** las direcciones de broadcast (255.255.255.255 +
  la de cada interfaz) y espera la respuesta en el mismo socket.
- Al recibir el `DISCOVER`, el servidor **registra al encuestador/dispositivo** (aparece en
  la pestaña *Dispositivos* de la central) y responde con su IP:puerto.

## 2. Pasivo (fallback): anuncio periódico

El servidor emite cada 3 s por broadcast en **todas** las interfaces:

```json
{ "type": "DISCOVERY", "service": "ENCUESTAS", "ip": "AUTO", "port": 5000 }
```

Android puede escuchar este anuncio en el puerto 8888 si el activo no obtuvo respuesta.
`"ip": "AUTO"` ⇒ usar la IP de origen del datagrama.

## 3. Manual (último recurso)

La central muestra sus **IPv4 locales** al iniciar el servidor. Si nada se autodetecta,
se escribe esa IP en el campo **"IP manual"** de la app Android.

## Requisitos de red

- Teléfono y central en la **misma red WiFi/LAN**.
- Firewall: el **MSI abre** automáticamente TCP 5000 (API) y UDP 8888 (discovery) en el
  ámbito de red local. Si se ejecuta el `.exe` sin instalar, Windows pedirá permiso la
  primera vez.

## Implementación

- Servidor (C#): `windows-app/src/EncuestasCentral/Discovery/DiscoveryService.cs`
- Android (Kotlin): `android-app/app/src/main/java/com/encuestas/offline/data/discovery/DiscoveryClient.kt`

> Nota: mDNS/Zeroconf queda como mejora futura; el protocolo UDP request/response cubre el
> mismo caso de uso sin dependencias adicionales.
