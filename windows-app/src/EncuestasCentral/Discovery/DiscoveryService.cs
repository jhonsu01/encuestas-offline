using System.Net;
using System.Net.Sockets;
using System.Text;

namespace EncuestasCentral.Discovery;

/// <summary>
/// Publica la presencia de la central en la LAN por UDP broadcast (puerto 8888),
/// como fallback al descubrimiento mDNS. Emite cada 3 segundos.
/// </summary>
public class DiscoveryService
{
    private readonly int _servicePort;
    private CancellationTokenSource? _cts;

    public DiscoveryService(int servicePort = 5000) => _servicePort = servicePort;

    public bool Running => _cts is { IsCancellationRequested: false };

    public void Start()
    {
        if (Running) return;
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => BroadcastLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private async Task BroadcastLoopAsync(CancellationToken ct)
    {
        using var udp = new UdpClient { EnableBroadcast = true };
        var endpoint = new IPEndPoint(IPAddress.Broadcast, 8888);
        var payload = Encoding.UTF8.GetBytes(
            $"{{\"type\":\"DISCOVERY\",\"service\":\"ENCUESTAS\",\"ip\":\"AUTO\",\"port\":{_servicePort}}}");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await udp.SendAsync(payload, payload.Length, endpoint);
                await Task.Delay(3000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Interfaz sin broadcast / transitorio: reintenta.
                try { await Task.Delay(3000, ct); } catch { break; }
            }
        }
    }
}
