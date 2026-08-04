using System;
using System.Linq;
using SharpPcap;
using PacketDotNet;

namespace AlbionBot.Network;

public class UdpSniffer
{
    private readonly PacketBufferQueue _queue;
    private ILiveDevice? _device;
    private bool _isSniffing;

    public UdpSniffer(PacketBufferQueue queue)
    {
        _queue = queue;
    }

    public void Start(string? deviceName = null)
    {
        var devices = CaptureDeviceList.Instance;
        if (devices.Count == 0)
        {
            throw new InvalidOperationException("No network capture devices found. Please ensure Npcap is installed.");
        }

        if (string.IsNullOrEmpty(deviceName))
        {
            _device = SelectPreferredDevice(devices);
        }
        else
        {
            _device = devices.FirstOrDefault(dev =>
                dev.Name.Equals(deviceName, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(dev.Description) && dev.Description.Contains(deviceName, StringComparison.OrdinalIgnoreCase)));

            if (_device == null)
            {
                _device = devices.FirstOrDefault(dev =>
                    !string.IsNullOrEmpty(dev.Description) &&
                    dev.Description.IndexOf(deviceName, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            _device ??= SelectPreferredDevice(devices);
        }

        var device = _device ?? throw new InvalidOperationException("Failed to select a valid capture device.");
        var deviceDescription = !string.IsNullOrEmpty(device.Description) ? device.Description : device.Name;
        Console.WriteLine(string.IsNullOrEmpty(deviceName)
            ? $"No device specified. Using default device: {deviceDescription}"
            : $"Selected device: {deviceDescription}");

        device.OnPacketArrival += OnPacketArrival;
        device.Open(DeviceModes.Promiscuous, 1000);
        device.Filter = "udp port 5055 or udp port 5056 or udp port 5058";
        device.StartCapture();
        _isSniffing = true;
    }

    public void Stop()
    {
        if (_device != null && _isSniffing)
        {
            _device.StopCapture();
            _device.Close();
            _isSniffing = false;
        }
    }

    private ILiveDevice? SelectPreferredDevice(CaptureDeviceList devices)
    {
        var preferredKeywords = new[] { "Killer", "Wi-Fi", "Wireless", "WLAN", "Intel", "Realtek", "Broadcom" };

        var candidate = devices.FirstOrDefault(d =>
            !string.IsNullOrEmpty(d.Description) &&
            preferredKeywords.Any(keyword => d.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase)));

        if (candidate != null)
        {
            return candidate;
        }

        candidate = devices.FirstOrDefault(d =>
            !string.IsNullOrEmpty(d.Description) &&
            d.Description.Contains("Ethernet", StringComparison.OrdinalIgnoreCase));

        if (candidate != null)
        {
            return candidate;
        }

        return devices.FirstOrDefault(d =>
            !string.IsNullOrEmpty(d.Description) &&
            !d.Description.Contains("Loopback", StringComparison.OrdinalIgnoreCase) &&
            !d.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
            !d.Description.Contains("WAN Miniport", StringComparison.OrdinalIgnoreCase))
            ?? devices[0];
    }

    private void OnPacketArrival(object sender, PacketCapture e)
    {
        var rawPacket = e.GetPacket();
        var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
        var udpPacket = packet.Extract<UdpPacket>();

        if (udpPacket != null && udpPacket.PayloadData != null && udpPacket.PayloadData.Length > 0)
        {
            _queue.TryEnqueue(udpPacket.PayloadData);
        }
    }
}
