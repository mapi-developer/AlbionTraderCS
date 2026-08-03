using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using PhotonPackageParser;

namespace AlbionBot.Photon;

public class AlbionParser : PhotonParser
{
    private const byte OP_AUCTION_GET_OFFERS = 75;
    private const byte OP_AUCTION_GET_REQUESTS = 76;

    // FIX: Updated signature to accept OperationResponse object
    protected override void OnResponse(OperationResponse response)
    {
        if (response.OperationCode == OP_AUCTION_GET_OFFERS)
        {
            Console.WriteLine("\n[Market] Intercepted Auction Offers!");
            ParseMarketData(response.Parameters);
        }
        else if (response.OperationCode == OP_AUCTION_GET_REQUESTS)
        {
            Console.WriteLine("\n[Market] Intercepted Auction Requests!");
            ParseMarketData(response.Parameters);
        }
    }

    // FIX: Updated signature to accept EventData object
    protected override void OnEvent(EventData eventData)
    {
        if (eventData.Code == 81) // Silver Update
        {
            if (eventData.Parameters.TryGetValue(1, out var silverAmount))
            {
                long trueSilver = Convert.ToInt64(silverAmount) / 10000;
                Console.WriteLine($"[Player] Silver Updated: {trueSilver:N0}");
            }
        }
    }

    // FIX: Updated signature to accept OperationRequest object
    protected override void OnRequest(OperationRequest request)
    {
        // Not needed for simple market scraping
    }

    private void ParseMarketData(Dictionary<byte, object> parameters)
    {
        // Market data is usually stored at key '0'
        if (parameters.TryGetValue(0, out var rawData))
        {
            // Scenario 1: Uncompressed JSON Array
            if (rawData is string[] jsonArray)
            {
                foreach (var jsonString in jsonArray)
                {
                    Console.WriteLine($"[Data]: {jsonString}");
                }
            }
            // Scenario 2: GZip Compressed Byte Array (Albion does this for large queries)
            else if (rawData is byte[] compressedBytes)
            {
                try
                {
                    string decompressedJson = DecompressGzip(compressedBytes);
                    Console.WriteLine($"[Decompressed Data]: {decompressedJson}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GZip Error]: {ex.Message}");
                }
            }
        }
    }

    private string DecompressGzip(byte[] data)
    {
        // Check for GZip magic numbers (1F 8B)
        if (data.Length >= 2 && data[0] == 0x1F && data[1] == 0x8B)
        {
            using var compressedStream = new MemoryStream(data);
            using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            gzipStream.CopyTo(resultStream);
            return Encoding.UTF8.GetString(resultStream.ToArray());
        }
        return Encoding.UTF8.GetString(data);
    }
}