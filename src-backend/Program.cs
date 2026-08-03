private static FragmentBuffer _fragBuffer = new FragmentBuffer();

private static void Sniffer_OnPhotonPacketReceived(object sender, byte[] payload)
{
    try 
    {
        // 1. Unpack the UDP payload into a Photon Layer
        var layer = PhotonLayer.Unpack(payload);

        // 2. Process each command inside the layer
        foreach (var cmd in layer.Commands)
        {
            if (cmd.Type == CommandType.SendReliableType)
            {
                ProcessReliableCommand(cmd.Data);
            }
            else if (cmd.Type == CommandType.SendReliableFragmentType)
            {
                var reassembledCmd = _fragBuffer.Offer(cmd);
                if (reassembledCmd != null)
                {
                    ProcessReliableCommand(reassembledCmd.Data);
                }
            }
        }
    }
    catch (Exception ex)
    {
        // Corrupted packet parsing fail
    }
}

private static void ProcessReliableCommand(byte[] reliableData)
{
    // At this point, reliableData contains the GZip compressed (or raw) Photon Protocol 16 Dictionary!
    // Next step: Decompress and parse the Event/Operation Dictionary.
    Console.WriteLine($"Reliable Command ready to be parsed! Size: {reliableData.Length}");
}