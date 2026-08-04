using System;

namespace AlbionBot.Infrastructure;

public static class DebugLogger
{
    public static bool Enabled { get; set; } = true;

    public static void Log(string message)
    {
        if (!Enabled)
        {
            return;
        }

        Console.WriteLine($"[DEBUG] {DateTime.UtcNow:HH:mm:ss.fff} - {message}");
    }

    public static void Enter(string method)
    {
        Log($"ENTER {method}");
    }

    public static void Exit(string method)
    {
        Log($"EXIT {method}");
    }
}
