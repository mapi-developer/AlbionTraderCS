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

        // Auto-select physical adapter
        _device = devices.FirstOrDefault(d => 
            !d.Description.Contains("Loopback") && 
            !d.Description.Contains("Virtual") &&
            !d.Description.Contains("WAN Miniport")) ?? devices[0];

        Console.WriteLine($"[Sniffer] Listening on: {_device.Description}");

        _device.Open(DeviceModes.Promiscuous, 1000);
        _device.Filter = "udp portrange 5055-5056";
        _device.OnPacketArrival += (sender, e) =>
        {
            // DEBUG STEP 1: Packet hit the adapter
            // Console.Write("."); // Uncomment if you want visual spam for every packet

            var rawPacket = e.GetPacket();
            var parsedPacket = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
            var udpPacket = parsedPacket.Extract<UdpPacket>();

            if (udpPacket != null)
            {
                if (udpPacket.PayloadData != null && udpPacket.PayloadData.Length > 0)
                {
                    // DEBUG STEP 2: UDP Payload extracted successfully
                    Console.WriteLine($"\n[Sniffer] Captured UDP Packet! Payload Size: {udpPacket.PayloadData.Length} bytes.");
                    OnUdpPayloadCaptured?.Invoke(udpPacket.PayloadData);
                }
                else
                {
                    Console.WriteLine("\n[Sniffer Warning] Packet caught, but UDP payload was empty.");
                }
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