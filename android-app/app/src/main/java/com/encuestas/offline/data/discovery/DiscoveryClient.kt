package com.encuestas.offline.data.discovery

import android.content.Context
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import android.net.wifi.WifiManager
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeoutOrNull
import org.json.JSONObject
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetSocketAddress
import kotlin.coroutines.resume

data class ServerInfo(val host: String, val port: Int, val method: String)

/**
 * Autodetección del servidor central:
 *   1) mDNS (_encuestas._tcp.local)
 *   2) UDP broadcast (puerto 8888) como fallback
 */
class DiscoveryClient(private val context: Context) {

    suspend fun discover(mdnsTimeoutMs: Long = 4000, udpTimeoutMs: Int = 5000): ServerInfo? =
        withContext(Dispatchers.IO) {
            val multicastLock = acquireMulticastLock()
            try {
                discoverMdns(mdnsTimeoutMs) ?: discoverUdp(udpTimeoutMs)
            } finally {
                runCatching { multicastLock?.release() }
            }
        }

    private fun acquireMulticastLock(): WifiManager.MulticastLock? = runCatching {
        val wifi = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
        wifi.createMulticastLock("encuestas-discovery").apply {
            setReferenceCounted(true)
            acquire()
        }
    }.getOrNull()

    private suspend fun discoverMdns(timeoutMs: Long): ServerInfo? = withTimeoutOrNull(timeoutMs) {
        suspendCancellableCoroutine { cont ->
            val nsd = context.getSystemService(Context.NSD_SERVICE) as NsdManager

            val resolveListener = object : NsdManager.ResolveListener {
                override fun onResolveFailed(serviceInfo: NsdServiceInfo?, errorCode: Int) {
                    if (cont.isActive) cont.resume(null)
                }
                override fun onServiceResolved(serviceInfo: NsdServiceInfo) {
                    val host = serviceInfo.host?.hostAddress
                    if (cont.isActive) {
                        cont.resume(host?.let { ServerInfo(it, serviceInfo.port, "mdns") })
                    }
                }
            }

            val discoveryListener = object : NsdManager.DiscoveryListener {
                override fun onStartDiscoveryFailed(serviceType: String?, errorCode: Int) {}
                override fun onStopDiscoveryFailed(serviceType: String?, errorCode: Int) {}
                override fun onDiscoveryStarted(serviceType: String?) {}
                override fun onDiscoveryStopped(serviceType: String?) {}
                override fun onServiceFound(serviceInfo: NsdServiceInfo) {
                    runCatching { nsd.resolveService(serviceInfo, resolveListener) }
                }
                override fun onServiceLost(serviceInfo: NsdServiceInfo?) {}
            }

            runCatching {
                nsd.discoverServices("_encuestas._tcp.", NsdManager.PROTOCOL_DNS_SD, discoveryListener)
            }
            cont.invokeOnCancellation {
                runCatching { nsd.stopServiceDiscovery(discoveryListener) }
            }
        }
    }

    private fun discoverUdp(timeoutMs: Int): ServerInfo? = runCatching {
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
            val msg = String(packet.data, 0, packet.length)
            val json = JSONObject(msg)
            if (json.optString("service") == "ENCUESTAS") {
                val ip = packet.address.hostAddress ?: return@runCatching null
                val port = json.optInt("port", 5000)
                ServerInfo(ip, port, "udp")
            } else null
        }
    }.getOrNull()
}
