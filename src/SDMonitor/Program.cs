using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using SDMonitor.Dashboard;
using SDMonitor.Devices;
using SDMonitor.Rendering;

namespace SDMonitor
{
    [ExcludeFromCodeCoverage]
    public class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] is "-h" or "--help" or "help")
            {
                PrintHelp();
                return 0;
            }

            try
            {
                var command = args.Length == 0 ? "dashboard" : args[0];
                switch (command)
                {
                    case "list":
                        ListDevices();
                        return 0;
                    case "info":
                        using (var deck = MonitorMini.OpenFirst())
                        {
                            PrintInfo(deck);
                        }

                        return 0;
                    case "brightness":
                        using (var deck = MonitorMini.OpenFirst())
                        {
                            deck.SetBrightness(ParseByte(args, 1, "percent", 0, 100));
                        }

                        return 0;
                    case "sleep":
                        using (var deck = MonitorMini.OpenFirst())
                        {
                            var seconds = ParseInt(args, 1, "seconds", 0, int.MaxValue);
                            deck.SetSleepDuration(TimeSpan.FromSeconds(seconds));
                        }

                        return 0;
                    case "logo":
                        using (var deck = MonitorMini.OpenFirst())
                        {
                            deck.ShowLogo();
                        }

                        return 0;
                    case "clear":
                        using (var deck = MonitorMini.OpenFirst())
                        {
                            deck.ClearKeys();
                        }

                        return 0;
                    case "color":
                        using (var deck = MonitorMini.OpenFirst())
                        {
                            var key = ParseInt(args, 1, "key", 0, MonitorMiniConstants.KeyCount - 1);
                            var red = ParseByte(args, 2, "red", 0, 255);
                            var green = ParseByte(args, 3, "green", 0, 255);
                            var blue = ParseByte(args, 4, "blue", 0, 255);
                            deck.SetKeyColor(key, red, green, blue);
                        }

                        return 0;
                    case "watch":
                        using (var deck = MonitorMini.OpenFirst())
                        {
                            Watch(deck);
                        }

                        return 0;
                    case "dashboard":
                        RunDashboard(args);
                        return 0;
                    case "layout-test":
                        using (var deck = MonitorMini.OpenFirst())
                        {
                            RenderLayoutTest(deck);
                        }

                        return 0;
                    default:
                        Console.Error.WriteLine($"Unknown command: {command}");
                        Console.Error.WriteLine("Run 'sdmonitor --help' for usage.");
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static void ListDevices()
        {
            var devices = MonitorMini.ListDevices();
            if (devices.Count == 0)
            {
                Console.WriteLine("No Stream Deck Mini HID devices found.");
                return;
            }

            foreach (var device in devices)
            {
                Console.WriteLine($"{device.ProductName} {device.VendorId:X4}:{device.ProductId:X4} serial={device.SerialNumber} path={device.DevicePath}");
            }
        }

        private static void PrintInfo(MonitorMini deck)
        {
            var info = deck.DeviceInfo;
            Console.WriteLine($"Product: {info.ProductName}");
            Console.WriteLine($"Manufacturer: {info.Manufacturer}");
            Console.WriteLine($"VID/PID: {info.VendorId:X4}:{info.ProductId:X4}");
            Console.WriteLine($"Path: {info.DevicePath}");
            Console.WriteLine($"Serial: {deck.GetSerialNumber()}");
            Console.WriteLine($"Firmware LD: {deck.GetFirmwareVersion(0xA0)}");
            Console.WriteLine($"Firmware AP2: {deck.GetFirmwareVersion(0xA1)}");
            Console.WriteLine($"Firmware AP1: {deck.GetFirmwareVersion(0xA2)}");
        }

        private static void Watch(MonitorMini deck)
        {
            Console.WriteLine("Watching key changes. Press Ctrl+C to stop.");
            while (true)
            {
                var states = deck.PollKeys();
                if (states is null)
                {
                    continue;
                }

                Console.WriteLine(string.Join(' ', states.Select(state => $"{state.KeyIndex}:{(state.IsPressed ? "down" : "up")}")));
            }
        }

        private static void RunDashboard(string[] args)
        {
            (string? configPath, int? refreshOverrideMilliseconds, int? frames) = ParseDashboardArguments(args);
            var config = DashboardConfig.Load(configPath, refreshOverrideMilliseconds);

            try
            {
                using var deck = MonitorMini.OpenFirst();
                DashboardRunner.Run(deck, config, frames);
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"Device unavailable: {ex.Message}");
                DashboardRunner.RunConsoleOnly(config, frames);
            }
        }

        private static (string? ConfigPath, int? RefreshOverrideMilliseconds, int? Frames) ParseDashboardArguments(string[] args)
        {
            if (args.Length > 3)
            {
                throw new ArgumentException("Invalid dashboard arguments.");
            }

            string? configPath = null;
            int? refreshOverrideMilliseconds = null;
            int? frames = args.Length > 2
                ? ParseInt(args, 2, "frames", 1, int.MaxValue)
                : null;

            if (args.Length <= 1)
            {
                return (configPath, refreshOverrideMilliseconds, frames);
            }

            if (int.TryParse(args[1], out _))
            {
                refreshOverrideMilliseconds = ParseInt(args, 1, "interval-ms", 250, 60000);
                return (configPath, refreshOverrideMilliseconds, frames);
            }

            configPath = args[1];
            return (configPath, refreshOverrideMilliseconds, frames);
        }

        private static void RenderLayoutTest(MonitorMini deck)
        {
            DashboardMetric[] labels =
            [
                new("cpu", "CPU", "CPU", 100, new RgbColor(0, 200, 255)),
                new("ram", "RAM", "RAM", 100, new RgbColor(120, 220, 80)),
                new("gpu", "GPU", "GPU", 100, new RgbColor(170, 130, 255)),
                new("disk", "HDD", "HDD", 100, new RgbColor(255, 190, 70)),
                new("upload", "UP", "UP", 100, new RgbColor(255, 100, 100)),
                new("download", "DOWN", "DOWN", 100, new RgbColor(80, 170, 255))
            ];

            for (var key = 0; key < labels.Length; key++)
            {
                var label = labels[key];
                deck.SetKeyImage(key, KeyTileRenderer.RenderLabelTile(label.Title, label.Accent));
                Thread.Sleep(50);
            }
        }

        private static byte ParseByte(string[] args, int index, string name, byte min, byte max)
        {
            var value = ParseInt(args, index, name, min, max);
            return checked((byte)value);
        }

        private static int ParseInt(string[] args, int index, string name, int min, int max)
        {
            if (args.Length <= index || !int.TryParse(args[index], out var value) || value < min || value > max)
            {
                throw new ArgumentException($"Invalid {name}. Expected integer from {min} to {max}.");
            }

            return value;
        }

        private static void PrintHelp()
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
                                dashboard [config-path|interval-ms] [frames]
                                layout-test
                              """);
        }
    }
}
