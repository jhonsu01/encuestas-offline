package com.encuestas.offline.data.discovery

import android.content.Context
import android.net.wifi.WifiManager
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.NetworkInterface
import java.net.SocketTimeoutException

data class ServerInfo(val host: String, val port: Int, val method: String)

/**
 * Autodetección del servidor central por UDP (puerto 8888):
 *   1) ACTIVO (principal): envía "DISCOVER" por broadcast en TODAS las interfaces y
 *      espera la respuesta UNICAST del servidor. Además, así el servidor registra
 *      al encuestador/dispositivo en la red.
 *   2) PASIVO (fallback): escucha el broadcast periódico del servidor en el 8888.
 */
class DiscoveryClient(private val context: Context) {

    suspend fun discover(
        deviceId: String,
        name: String,
        surveyorId: String,
        timeoutMs: Int = 5000
    ): ServerInfo? = withContext(Dispatchers.IO) {
        val lock = acquireMulticastLock()
        try {
            discoverActive(deviceId, name, surveyorId, timeoutMs) ?: discoverPassive(timeoutMs)
        } finally {
            runCatching { lock?.release() }
        }
    }

    private fun acquireMulticastLock(): WifiManager.MulticastLock? = runCatching {
        val wifi = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
        wifi.createMulticastLock("encuestas-discovery").apply {
            setReferenceCounted(true)
            acquire()
        }
    }.getOrNull()

    private fun discoverActive(deviceId: String, name: String, surveyorId: String, timeoutMs: Int): ServerInfo? =
        runCatching {
            DatagramSocket().use { socket ->
                socket.broadcast = true
                socket.soTimeout = 1000
                val req = JSONObject()
                    .put("type", "DISCOVER")
                    .put("role", "device")
                    .put("deviceId", deviceId)
                    .put("name", name)
                    .put("surveyorId", surveyorId)
                    .toString().toByteArray()

                val buf = ByteArray(2048)
                val deadline = System.currentTimeMillis() + timeoutMs
                while (System.currentTimeMillis() < deadline) {
                    for (addr in broadcastAddresses()) {
                        runCatching { socket.send(DatagramPacket(req, req.size, addr, 8888)) }
                    }
                    try {
                        val packet = DatagramPacket(buf, buf.size)
                        socket.receive(packet)
                        val json = JSONObject(String(packet.data, 0, packet.length))
                        if (json.optString("type") == "SERVER" || json.optString("service") == "ENCUESTAS") {
                            val ip = packet.address.hostAddress ?: continue
                            return@use ServerInfo(ip, json.optInt("port", 5000), "udp")
                        }
                    } catch (_: SocketTimeoutException) {
                        // sin respuesta aún; reintenta el ciclo
                    }
                }
                null
            }
        }.getOrNull()

    private fun discoverPassive(timeoutMs: Int): ServerInfo? = runCatching {
        val socket = DatagramSocket(null).apply {
            reuseAddress = true
            broadcast = true
            soTimeout = timeoutMs
            bind(InetSocketAddress(8888))
        }
        socket.use { s ->
            val buf = ByteArray(2048)
            val packet = DatagramPacket(buf, buf.size)
            s.receive(packet)
            val json = JSONObject(String(packet.data, 0, packet.length))
            if (json.optString("service") == "ENCUESTAS") {
                val ip = packet.address.hostAddress ?: return@runCatching null
                ServerInfo(ip, json.optInt("port", 5000), "broadcast")
            } else null
        }
    }.getOrNull()

    /** Direcciones de broadcast de todas las interfaces + la global 255.255.255.255. */
    private fun broadcastAddresses(): List<InetAddress> {
        val result = mutableListOf<InetAddress>()
        runCatching { result.add(InetAddress.getByName("255.255.255.255")) }
        runCatching {
            for (ni in NetworkInterface.getNetworkInterfaces()) {
                if (!ni.isUp || ni.isLoopback) continue
                for (ia in ni.interfaceAddresses) {
                    ia.broadcast?.let { result.add(it) }
                }
            }
        }
        return result.distinctBy { it.hostAddress ?: it.toString() }
    }
}
