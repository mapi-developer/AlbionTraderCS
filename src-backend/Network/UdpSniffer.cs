using System;
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
            _device = devices[0];
        }
        else
        {
            foreach (var dev in devices)
            {
                if (dev.Name.Equals(deviceName, StringComparison.OrdinalIgnoreCase) ||
                    (dev.Description != null && dev.Description.Contains(deviceName, StringComparison.OrdinalIgnoreCase)))
                {
                    _device = dev;
                    break;
                }
            }
            _device ??= devices[0];
        }

        _device.OnPacketArrival += OnPacketArrival;
        _device.Open(DeviceModes.Promiscuous, 1000);
        _device.Filter = "udp port 5055 or udp port 5056 or udp port 5058";
        _device.StartCapture();
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
