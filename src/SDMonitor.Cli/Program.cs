using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SDMonitor.Control;
using SDMonitor.Cli;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintHelp();
    return 0;
}

try
{
    switch (args[0])
    {
        case "list":
            ListDevices();
            return 0;
        case "info":
            using (SdMonitorMini deck = SdMonitorMini.OpenFirst())
            {
                PrintInfo(deck);
            }
            return 0;
        case "brightness":
            using (SdMonitorMini deck = SdMonitorMini.OpenFirst())
            {
                deck.SetBrightness(ParseByte(args, 1, "percent", 0, 100));
            }
            return 0;
        case "sleep":
            using (SdMonitorMini deck = SdMonitorMini.OpenFirst())
            {
                int seconds = ParseInt(args, 1, "seconds", 0, int.MaxValue);
                deck.SetSleepDuration(TimeSpan.FromSeconds(seconds));
            }
            return 0;
        case "logo":
            using (SdMonitorMini deck = SdMonitorMini.OpenFirst())
            {
                deck.ShowLogo();
            }
            return 0;
        case "clear":
            using (SdMonitorMini deck = SdMonitorMini.OpenFirst())
            {
                deck.ClearKeys();
            }
            return 0;
        case "color":
            using (SdMonitorMini deck = SdMonitorMini.OpenFirst())
            {
                int key = ParseInt(args, 1, "key", 0, SdMonitorMiniConstants.KeyCount - 1);
                byte red = ParseByte(args, 2, "red", 0, 255);
                byte green = ParseByte(args, 3, "green", 0, 255);
                byte blue = ParseByte(args, 4, "blue", 0, 255);
                deck.SetKeyColor(key, red, green, blue);
            }
            return 0;
        case "watch":
            using (SdMonitorMini deck = SdMonitorMini.OpenFirst())
            {
                Watch(deck);
            }
            return 0;
        case "dashboard":
            using (SdMonitorMini deck = SdMonitorMini.OpenFirst())
            {
                int intervalMilliseconds = args.Length > 1
                    ? ParseInt(args, 1, "interval-ms", 250, 60000)
                    : 1500;
                int? frames = args.Length > 2
                    ? ParseInt(args, 2, "frames", 1, int.MaxValue)
                    : null;
                DashboardRunner.Run(deck, intervalMilliseconds, frames);
            }
            return 0;
        case "layout-test":
            using (SdMonitorMini deck = SdMonitorMini.OpenFirst())
            {
                RenderLayoutTest(deck);
            }
            return 0;
        default:
            Console.Error.WriteLine($"Unknown command: {args[0]}");
            PrintHelp();
            return 2;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static void ListDevices()
{
    IReadOnlyList<SdMonitorDeviceInfo> devices = SdMonitorMini.ListDevices();
    if (devices.Count == 0)
    {
        Console.WriteLine("No Stream Deck Mini HID devices found.");
        return;
    }

    foreach (SdMonitorDeviceInfo device in devices)
    {
        Console.WriteLine($"{device.ProductName} {device.VendorId:X4}:{device.ProductId:X4} serial={device.SerialNumber} path={device.DevicePath}");
    }
}

static void PrintInfo(SdMonitorMini deck)
{
    SdMonitorDeviceInfo info = deck.DeviceInfo;
    Console.WriteLine($"Product: {info.ProductName}");
    Console.WriteLine($"Manufacturer: {info.Manufacturer}");
    Console.WriteLine($"VID/PID: {info.VendorId:X4}:{info.ProductId:X4}");
    Console.WriteLine($"Path: {info.DevicePath}");
    Console.WriteLine($"Serial: {deck.GetSerialNumber()}");
    Console.WriteLine($"Firmware LD: {deck.GetFirmwareVersion(0xA0)}");
    Console.WriteLine($"Firmware AP2: {deck.GetFirmwareVersion(0xA1)}");
    Console.WriteLine($"Firmware AP1: {deck.GetFirmwareVersion(0xA2)}");
}

static void Watch(SdMonitorMini deck)
{
    Console.WriteLine("Watching key changes. Press Ctrl+C to stop.");
    while (true)
    {
        IReadOnlyList<MiniKeyState>? states = deck.PollKeys();
        if (states is null)
        {
            continue;
        }

        Console.WriteLine(string.Join(' ', states.Select(state => $"{state.KeyIndex}:{(state.IsPressed ? "down" : "up")}")));
    }
}

static void RenderLayoutTest(SdMonitorMini deck)
{
    DashboardMetric[] labels =
    [
        new("CPU", "CPU", 100, new RgbColor(0, 200, 255)),
        new("RAM", "RAM", 100, new RgbColor(120, 220, 80)),
        new("GPU", "GPU", 100, new RgbColor(170, 130, 255)),
        new("HDD", "HDD", 100, new RgbColor(255, 190, 70)),
        new("UP", "UP", 100, new RgbColor(255, 100, 100)),
        new("DOWN", "DOWN", 100, new RgbColor(80, 170, 255))
    ];

    for (int key = 0; key < labels.Length; key++)
    {
        DashboardMetric label = labels[key];
        deck.SetKeyImage(key, KeyTileRenderer.RenderLabelTile(label.Title, label.Accent));
        Thread.Sleep(50);
    }
}

static byte ParseByte(string[] args, int index, string name, byte min, byte max)
{
    int value = ParseInt(args, index, name, min, max);
    return checked((byte)value);
}

static int ParseInt(string[] args, int index, string name, int min, int max)
{
    if (args.Length <= index || !int.TryParse(args[index], out int value) || value < min || value > max)
    {
        throw new ArgumentException($"Invalid {name}. Expected integer from {min} to {max}.");
    }

    return value;
}

static void PrintHelp()
{
    Console.WriteLine("""
    sdmonitor command:
      list
      info
      brightness <0-100>
      sleep <seconds>
      logo
      clear
      color <key 0-5> <r 0-255> <g 0-255> <b 0-255>
      watch
      dashboard [interval-ms] [frames]
      layout-test
    """);
}
