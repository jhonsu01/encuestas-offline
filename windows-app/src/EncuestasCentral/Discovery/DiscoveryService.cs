using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace EncuestasCentral.Discovery;

/// <summary>Un dispositivo/encuestador detectado en la red.</summary>
public class DeviceSeen
{
    public string DeviceId { get; set; } = "";
    public string Name { get; set; } = "";
    public string SurveyorId { get; set; } = "";
    public string Ip { get; set; } = "";
}

/// <summary>
/// Descubrimiento en LAN por UDP (puerto 8888), bidireccional:
///  - Emite un anuncio DISCOVERY por broadcast en TODAS las interfaces (cada 3 s).
///  - Escucha peticiones DISCOVER de los dispositivos, los registra y responde
///    UNICAST con la info del servidor (mucho más fiable que solo emitir).
/// </summary>
public class DiscoveryService
{
    private const int DiscoveryPort = 8888;
    private readonly int _servicePort;
    private CancellationTokenSource? _cts;

    public event Action<DeviceSeen>? OnDeviceSeen;

    public DiscoveryService(int servicePort = 5000) => _servicePort = servicePort;

    public bool Running => _cts is { IsCancellationRequested: false };

    public void Start()
    {
        if (Running) return;
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => BroadcastLoopAsync(_cts.Token));
        _ = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    /// <summary>Direcciones de broadcast de todas las interfaces IPv4 activas (+ la global).</summary>
    public static List<IPEndPoint> BroadcastTargets(int port)
    {
        var list = new List<IPEndPoint> { new(IPAddress.Broadcast, port) };
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (var ua in ni.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var mask = ua.IPv4Mask;
                if (mask == null || Equals(mask, IPAddress.Any)) continue;
                var ip = ua.Address.GetAddressBytes();
                var mb = mask.GetAddressBytes();
                var bc = new byte[4];
                for (int i = 0; i < 4; i++) bc[i] = (byte)(ip[i] | (mb[i] ^ 0xFF));
                list.Add(new IPEndPoint(new IPAddress(bc), port));
            }
        }
        return list;
    }

    /// <summary>IPv4 locales (no loopback) para mostrarlas al operador.</summary>
    public static List<string> LocalIPv4()
    {
        var result = new List<string>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (var ua in ni.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                    result.Add(ua.Address.ToString());
            }
        }
        return result;
    }

    private async Task BroadcastLoopAsync(CancellationToken ct)
    {
        using var udp = new UdpClient { EnableBroadcast = true };
        var payload = Encoding.UTF8.GetBytes(
            $"{{\"type\":\"DISCOVERY\",\"service\":\"ENCUESTAS\",\"ip\":\"AUTO\",\"port\":{_servicePort}}}");

        while (!ct.IsCancellationRequested)
        {
            foreach (var ep in BroadcastTargets(DiscoveryPort))
            {
                try { await udp.SendAsync(payload, payload.Length, ep); } catch { /* interfaz sin broadcast */ }
            }
            try { await Task.Delay(3000, ct); } catch { break; }
        }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        UdpClient server;
        try
        {
            server = new UdpClient();
            server.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            server.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
            server.EnableBroadcast = true;
        }
        catch
        {
            return; // Puerto ocupado: seguimos solo con broadcast.
        }

        using (server)
        {
            var reply = Encoding.UTF8.GetBytes(
                $"{{\"type\":\"SERVER\",\"service\":\"ENCUESTAS\",\"ip\":\"AUTO\",\"port\":{_servicePort}}}");

            while (!ct.IsCancellationRequested)
            {
                UdpReceiveResult res;
                try { res = await server.ReceiveAsync(ct); }
                catch { break; }

                try
                {
                    using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(res.Buffer));
                    var root = doc.RootElement;
                    var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
                    if (type != "DISCOVER") continue;

                    OnDeviceSeen?.Invoke(new DeviceSeen
                    {
                        DeviceId = Prop(root, "deviceId"),
                        Name = Prop(root, "name"),
                        SurveyorId = Prop(root, "surveyorId"),
                        Ip = res.RemoteEndPoint.Address.ToString()
                    });

                    // Respuesta UNICAST directa al dispositivo que preguntó.
                    try { await server.SendAsync(reply, reply.Length, res.RemoteEndPoint); } catch { }
                }
                catch { /* datagrama no-JSON, ignorar */ }
            }
        }
    }

    private static string Prop(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? (v.GetString() ?? "") : "";
}
