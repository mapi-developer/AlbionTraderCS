using System;
using System.Linq;
using SharpPcap;
using PacketDotNet;

namespace AlbionBot.Network;

public class Sniffer
{
    private ICaptureDevice? _device;
    
    public event Action<byte[]>? OnUdpPayloadCaptured;

    public void Start()
    {
        var devices = CaptureDeviceList.Instance;
        if (devices.Count == 0)
        {
            Console.WriteLine("[Sniffer Error] No network interfaces found. Please install Npcap.");
            return;
        }

        // Auto-select physical adapter (prioritizing your Killer Wi-Fi)
        _device = devices.FirstOrDefault(d => 
            !d.Description.Contains("Loopback") && 
            !d.Description.Contains("Virtual") &&
            !d.Description.Contains("WAN Miniport")) ?? devices[0];

        Console.WriteLine($"[Sniffer] Listening on: {_device.Description}");

        _device.Open(DeviceModes.Promiscuous, 1000);
        _device.Filter = "udp portrange 5055-5056";
        _device.OnPacketArrival += (sender, e) =>
        {
            var rawPacket = e.GetPacket();
            var parsedPacket = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
            var udpPacket = parsedPacket.Extract<UdpPacket>();

            if (udpPacket?.PayloadData != null && udpPacket.PayloadData.Length > 0)
            {
                OnUdpPayloadCaptured?.Invoke(udpPacket.PayloadData);
            }
        };

        _device.StartCapture();
    }

    public void Stop()
    {
        _device?.StopCapture();
        _device?.Close();
    }
}