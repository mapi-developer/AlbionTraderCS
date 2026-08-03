using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using SharpPcap;
using PacketDotNet;

// Added references to internal class libraries
using AlbionBot.Core.Enums;
using AlbionBot.Core.Models;
using AlbionBot.Network.Parsers;
using AlbionBot.Network.Buffers;

namespace AlbionBot.Network;

public class Sniffer : IDisposable
{
    private ICaptureDevice? _device;
    private Thread? _captureThread;
    private bool _isRunning;
    private bool _disposed;
    
    // Internal buffer to handle fragmented market packets
    private readonly FragmentBuffer _fragmentBuffer;

    /// <summary>
    /// Event fired when a reassembled, reliable Photon Command payload is ready for game-data extraction.
    /// </summary>
    public event EventHandler<byte[]>? OnReliableCommandReady;

    public Sniffer()
    {
        _fragmentBuffer = new FragmentBuffer();
    }

    /// <summary>
    /// Starts packet sniffing on the target device or auto-detected active adapter.
    /// </summary>
    /// <param name="targetKeyword">Optional search string (e.g. "Killer", "Realtek", "Wi-Fi").</param>
    public void Start(string? targetKeyword = null)
    {
        if (_isRunning)
        {
            Console.WriteLine("[Sniffer] Already running.");
            return;
        }

        var devices = CaptureDeviceList.Instance;
        if (devices == null || devices.Count == 0)
        {
            Console.WriteLine("[Sniffer Error] No network interfaces found. Ensure Npcap is installed.");
            return;
        }

        // 1. Resolve target device
        _device = ResolveDevice(devices, targetKeyword);

        if (_device == null)
        {
            Console.WriteLine("[Sniffer Error] Could not auto-detect an active network interface.");
            return;
        }

        Console.WriteLine($"[Sniffer] Target device selected: {_device.Description}");

        try
        {
            // 2. Open device in Promiscuous mode with a 1000ms read timeout
            _device.Open(DeviceModes.Promiscuous, 1000);

            // 3. Set Berkeley Packet Filter for Photon UDP ports
            _device.Filter = "udp portrange 5055-5056";
            Console.WriteLine($"[Sniffer] Filter active: {_device.Filter}");

            // 4. Hook packet handler
            _device.OnPacketArrival += Device_OnPacketArrival;

            // 5. Spawn dedicated low-latency capture thread
            _isRunning = true;
            _captureThread = new Thread(RunCaptureLoop)
            {
                Name = "AlbionSnifferThread",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _captureThread.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Sniffer Initialization Error]: {ex.Message}");
            Stop();
        }
    }

    private void RunCaptureLoop()
    {
        try
        {
            Console.WriteLine($"[Sniffer] Capturing packets on {_device?.Description}...");
            _device?.Capture();
        }
        catch (Exception ex)
        {
            if (_isRunning)
            {
                Console.WriteLine($"[Sniffer Thread Error]: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Stops the packet sniffer and releases interface handles.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;

        try
        {
            if (_device != null)
            {
                _device.OnPacketArrival -= Device_OnPacketArrival;
                _device.StopCapture();
                _device.Close();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Sniffer Close Exception]: {ex.Message}");
        }
        finally
        {
            _device = null;
        }

        if (_captureThread != null && _captureThread.IsAlive)
        {
            _captureThread.Join(TimeSpan.FromSeconds(2));
            _captureThread = null;
        }

        Console.WriteLine("[Sniffer] Stopped successfully.");
    }

    private void Device_OnPacketArrival(object sender, PacketCapture e)
    {
        if (!_isRunning) return;

        try
        {
            var rawPacket = e.GetPacket();
            var parsedPacket = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
            var udpPacket = parsedPacket.Extract<UdpPacket>();

            if (udpPacket != null)
            {
                var payload = udpPacket.PayloadData;
                if (payload != null && payload.Length > 0)
                {
                    ProcessPhotonPayload(payload);
                }
            }
        }
        catch
        {
            // Silently swallow parse errors on malformed/corrupted network packets
        }
    }

    /// <summary>
    /// Unpacks the raw UDP payload into a PhotonLayer and handles Command fragmentation.
    /// </summary>
    private void ProcessPhotonPayload(byte[] payload)
    {
        try 
        {
            var layer = PhotonLayer.Unpack(payload);

            foreach (var cmd in layer.Commands)
            {
                if (cmd.Type == CommandType.SendReliableType)
                {
                    OnReliableCommandReady?.Invoke(this, cmd.Data);
                }
                else if (cmd.Type == CommandType.SendReliableFragmentType)
                {
                    var reassembledCmd = _fragmentBuffer.Offer(cmd);
                    if (reassembledCmd != null)
                    {
                        OnReliableCommandReady?.Invoke(this, reassembledCmd.Data);
                    }
                }
            }
        }
        catch
        {
            // Packet parsing failed due to unexpected Photon format or encryption changes
        }
    }

    /// <summary>
    /// Smart adapter resolution using Keyword overrides, Active IPv4 checking, and physical device prioritization.
    /// </summary>
    private ICaptureDevice? ResolveDevice(CaptureDeviceList devices, string? targetKeyword)
    {
        if (!string.IsNullOrWhiteSpace(targetKeyword))
        {
            var match = devices.FirstOrDefault(d => d.Description.Contains(targetKeyword, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        string? activeIp = GetActiveLocalIPv4Address();
        if (!string.IsNullOrEmpty(activeIp))
        {
            foreach (var dev in devices)
            {
                // ADDED ?. to safely check if ToString() is null
                if (dev.ToString()?.Contains(activeIp) == true)
                {
                    Console.WriteLine($"[Sniffer] Auto-matched interface via active IP: {activeIp}");
                    return dev;
                }
            }
        }

        var physicalAdapter = devices.FirstOrDefault(d =>
            d.Description != null && // ADDED NULL CHECK HERE
            (d.Description.Contains("Killer", StringComparison.OrdinalIgnoreCase) ||
             d.Description.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
             d.Description.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
             d.Description.Contains("Ethernet", StringComparison.OrdinalIgnoreCase) ||
             d.Description.Contains("Realtek", StringComparison.OrdinalIgnoreCase) ||
             d.Description.Contains("Intel", StringComparison.OrdinalIgnoreCase)) &&
            !d.Description.Contains("WAN Miniport", StringComparison.OrdinalIgnoreCase) &&
            !d.Description.Contains("Virtual Adapter", StringComparison.OrdinalIgnoreCase) &&
            !d.Description.Contains("Loopback", StringComparison.OrdinalIgnoreCase)
        );

        return physicalAdapter ?? devices.FirstOrDefault();
    }

    private string? GetActiveLocalIPv4Address()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530); 
            if (socket.LocalEndPoint is IPEndPoint endPoint)
            {
                return endPoint.Address.ToString();
            }
        }
        catch
        {
            var activeInterface = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .FirstOrDefault(ni => ni.GetIPProperties().UnicastAddresses
                    .Any(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork));

            return activeInterface?.GetIPProperties().UnicastAddresses
                .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString();
        }

        return null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Stop();
            }
            _disposed = true;
        }
    }
}