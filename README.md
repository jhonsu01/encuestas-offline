# 📋 Encuestas Offline — Android + Windows (LAN, sin nube)

Sistema de recolección de encuestas **offline-first** con sincronización por **red local**
(WiFi/LAN), autodetección de servidor (mDNS + UDP broadcast) y transferencia por lotes.
**No usa ningún servicio en la nube ni requiere internet.**

[![Build & Release](https://github.com/jhonsu01/encuestas-offline/actions/workflows/release.yml/badge.svg)](https://github.com/jhonsu01/encuestas-offline/actions/workflows/release.yml)
[![Latest Release](https://img.shields.io/github/v/release/jhonsu01/encuestas-offline?label=última%20release)](https://github.com/jhonsu01/encuestas-offline/releases/latest)

---

## ⬇️ Descargar (listo para instalar)

Todos los binarios se compilan automáticamente en GitHub Actions y se publican en la
**[última release](https://github.com/jhonsu01/encuestas-offline/releases/latest)**:

| Plataforma | Archivo                          | Instalación                                        |
| ---------- | -------------------------------- | -------------------------------------------------- |
| 📱 Android | `encuestas-android-vX.Y.Z.apk`   | Copiar al teléfono → abrir → permitir orígenes desconocidos |
| 🖥️ Windows | `EncuestasCentral-vX.Y.Z.msi`    | Doble clic → siguiente → instalar                  |

> Cada archivo lleva el número de versión en el nombre (p. ej. `encuestas-android-v0.1.7.apk`).

> Solo se mantiene **la última release**; las anteriores se eliminan automáticamente
> en cada nuevo build (ver `.github/workflows/release.yml`).

---

## 🧱 Arquitectura

```
/android-app        App móvil del encuestador (Kotlin + Jetpack Compose + Room)
/windows-app        App central + servidor local embebido (.NET 8 WPF + ASP.NET Core)
/api-local          Contrato/documentación de la API REST local
/network-discovery  Protocolo de descubrimiento (mDNS + UDP broadcast)
/shared-schema      Contrato de datos común (formularios, lotes, firma SHA256)
/docs               Documentación técnica
```

Clean Architecture + separación de responsabilidades. Comunicación por REST API local.
Detalles en [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

---

## 🔄 Flujo de uso

1. **Windows (central):** el operador crea la encuesta en el editor, exporta el JSON y
   arranca el servidor local (mDNS `_encuestas._tcp.local` + broadcast UDP en el puerto 8888).
2. **Android (campo):** el encuestador se registra (CC/CE), la app **autodetecta** el
   servidor en la red, descarga la encuesta y captura datos **sin conexión**.
3. **Cierre diario:** el encuestador confirma el cierre con su **PIN local** y se genera
   un **PIN de sincronización** (6–8 dígitos) asociado al lote.
4. **Sincronización:** la app envía el lote a `POST /sync` validando por PIN; el servidor
   valida hash, duplicados e integridad y confirma transaccionalmente.
5. **Windows (análisis):** el dashboard muestra totales por encuestador, fecha y ubicación.

---

## 🛠️ Compilar localmente (opcional)

### Android (requiere Android SDK + JDK 17)
```bash
cd android-app
gradle assembleRelease        # o: ./gradlew assembleRelease
# APK en app/build/outputs/apk/release/
```

### Windows (requiere .NET 8 SDK)
```bash
cd windows-app
dotnet publish src/EncuestasCentral/EncuestasCentral.csproj -c Release -r win-x64 --self-contained
# Ejecutable en bin/Release/net8.0-windows/win-x64/publish/
```

El **APK firmado** y el **MSI** se generan sin tocar nada localmente vía GitHub Actions.

---

## 🔐 Seguridad

- Firma SHA256 por respuesta (integridad y anti-duplicados).
- PIN temporal de sincronización asociado al lote.
- Token de sesión para la API local.
- Lista blanca de dispositivos en la central.
- Base de datos Android protegida (ver `docs/ARCHITECTURE.md`).

---

## 🧪 Pruebas

- Android: pruebas unitarias de la firma y del motor de formularios (`app/src/test`).
- Windows: pruebas de la firma y de la recepción de lotes (`tests/`).

---

## 📄 Licencia

MIT © 2026 jhonsu01
