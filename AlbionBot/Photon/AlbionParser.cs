using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using AlbionBot.Models;

namespace AlbionBot.Photon;

public class AlbionParser : PhotonParser
{
    public AlbionParser()
    {
        this.OnMessageReady += HandleMessage;
    }

    private void HandleMessage(PhotonMessage message)
    {
        Console.WriteLine($"\n[Custom Parser SUCCESS] MessageType: {message.MessageType} | Code: {message.Code}");

        if (message.Parameters == null || message.Parameters.Count == 0)
        {
            Console.WriteLine("   -> [Debug] Parameters dictionary is EMPTY. (Deserializer might have failed silently)");
            return;
        }

        Console.WriteLine($"   -> [Debug] Parameters contain {message.Parameters.Count} keys.");

        // Dump ALL keys and their data types to see exactly where Albion is hiding the data
        foreach (var kvp in message.Parameters)
        {
            string typeName = kvp.Value != null ? kvp.Value.GetType().Name : "NULL";
            Console.WriteLine($"   -> Key: {kvp.Key} | Data Type: {typeName}");

            // Look for Uncompressed JSON Arrays (Matches your old working script logic)
            if (kvp.Value is string[] jsonArray)
            {
                Console.WriteLine($"      [!] FOUND STRING ARRAY! ({jsonArray.Length} items)");
                foreach (var jsonString in jsonArray)
                {
                    // Print first 300 chars to avoid flooding the console completely
                    Console.WriteLine($"      [Data]: {jsonString.Substring(0, Math.Min(jsonString.Length, 300))}...");
                }
            }
            // Look for Compressed GZip Byte Arrays (Albion's standard for huge market requests)
            else if (kvp.Value is byte[] compressedBytes)
            {
                Console.WriteLine($"      [!] FOUND BYTE ARRAY! ({compressedBytes.Length} bytes). Attempting Decompression...");
                try
                {
                    string decompressedJson = DecompressGzip(compressedBytes);
                    Console.WriteLine($"      [Decompressed Data]: {decompressedJson.Substring(0, Math.Min(decompressedJson.Length, 500))}...");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"      [GZip Error]: {ex.Message}");
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
        
        // If it's not GZipped, maybe it's just a raw JSON byte string
        return Encoding.UTF8.GetString(data); 
    }
}