using Spectre.Console;
using SX.Core;
using System.Text;

namespace SX.Server;

internal class Program
{
	private static async Task<int> Main(string[] args)
	{
		// Configure console for emoji support
		Console.OutputEncoding = Encoding.UTF8;
		AnsiConsole.Profile.Capabilities.Unicode = true;
		AnsiConsole.Profile.Capabilities.Ansi = true;
		AnsiConsole.Profile.Capabilities.ColorSystem = ColorSystem.TrueColor;

		var config = Configuration.Default;

		// Parse command line arguments
		for (var i = 0; i < args.Length; i++)
			switch (args[i])
			{
				case "--port" or "-p":
					if (i + 1 < args.Length && int.TryParse(args[i + 1], out var port))
					{
						config.Port = port;
						i++;
					}
					else
					{
						AnsiConsole.MarkupLine("[red]❌ Error: --port requires a valid number[/]");
						return 1;
					}

					break;

				case "--dir" or "-d":
					if (i + 1 < args.Length)
					{
						var dirPath = Path.GetFullPath(args[i + 1]);
						config.DownloadDirectory = dirPath;
						config.ServeDirectory = dirPath; // Use same directory for both download and serve
						i++;
					}
					else
					{
						AnsiConsole.MarkupLine("[red]❌ Error: --dir requires a directory path[/]");
						return 1;
					}

					break;

				case "--max-size":
					if (i + 1 < args.Length && ParseFileSize(args[i + 1], out var maxSize))
					{
						config.MaxFileSize = maxSize;
						i++;
					}
					else
					{
						AnsiConsole.MarkupLine("[red]❌ Error: --max-size requires a valid size (e.g., 100MB, 1GB)[/]");
						return 1;
					}

					break;

				case "--no-overwrite":
					config.AllowOverwrite = false;
					break;

				case "--help" or "-h":
					ShowHelp();
					return 0;

				default:
					AnsiConsole.MarkupLine($"[red]❌ Unknown option: {args[i]}[/]");
					ShowHelp();
					return 1;
			}

		AnsiConsole.MarkupLine("[yellow]🚀 SX - SSH File Transfer System[/]");
		AnsiConsole.MarkupLine("[yellow]=============================[/]");
		AnsiConsole.MarkupLine($"[blue]📁 File directory: {config.DownloadDirectory}[/]");

		config.EnsureServeDirectoryExists();
		var server = new FileTransferServer(config);

		// Handle Ctrl+C gracefully
		var cts = new CancellationTokenSource();
		var shutdownRequested = false;
		Console.CancelKeyPress += (_, e) =>
		{
			if (!shutdownRequested)
			{
				shutdownRequested = true;
				e.Cancel = true;
				AnsiConsole.MarkupLine("\n[yellow]⏹️  Shutting down...[/]");
				cts.Cancel();
			}
			else
			{
				// Force exit on second Ctrl+C
				Environment.Exit(1);
			}
		};

		try
		{
			await server.StartAsync(cts.Token);
		}
		catch (OperationCanceledException)
		{
			AnsiConsole.MarkupLine("[green]✅ Shutdown complete.[/]");
		}
		catch (Exception ex)
		{
			AnsiConsole.MarkupLine($"[red]❌ Error: {ex.Message}[/]");
			return 1;
		}
		finally
		{
			await server.StopAsync();
		}

		return 0;
	}

	private static void ShowHelp()
	{
		AnsiConsole.MarkupLine("[yellow]🚀 SX - SSH File Transfer System[/]");
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[white]Usage: sx [[options]][/]");
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[white]Options:[/]");
		AnsiConsole.MarkupLine("  [blue]-p, --port <port>[/]     Port to listen on (default: 53690)");
		AnsiConsole.MarkupLine("  [blue]-d, --dir <path>[/]      Download directory (default: ~/Downloads)");
		AnsiConsole.MarkupLine("  [blue]    --max-size <size>[/] Maximum file size (default: 100MB)");
		AnsiConsole.MarkupLine("  [blue]    --no-overwrite[/]    Don't overwrite existing files");
		AnsiConsole.MarkupLine("  [blue]-h, --help[/]            Show this help");
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[white]Examples:[/]");
		AnsiConsole.MarkupLine("  [dim]sx[/]");
		AnsiConsole.MarkupLine("  [dim]sx --port 9999 --dir /tmp/downloads[/]");
		AnsiConsole.MarkupLine("  [dim]sx --max-size 1GB --no-overwrite[/]");
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[white]Remote usage (after setting up SSH tunnel):[/]");
		AnsiConsole.MarkupLine(@"  [dim]echo -e 'FILENAME:test.txt\nSIZE:11\nDATA:\nHello World' | nc localhost 53690[/]");
	}

	private static bool ParseFileSize(string sizeStr, out long bytes)
	{
		bytes = 0;

		if (string.IsNullOrWhiteSpace(sizeStr))
			return false;

		var multiplier = 1L;
		var numStr = sizeStr;

		if (sizeStr.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
		{
			multiplier = 1024;
			numStr = sizeStr[..^2];
		}
		else if (sizeStr.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
		{
			multiplier = 1024 * 1024;
			numStr = sizeStr[..^2];
		}
		else if (sizeStr.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
		{
			multiplier = 1024 * 1024 * 1024;
			numStr = sizeStr[..^2];
		}

		if (long.TryParse(numStr, out var num) && num > 0)
		{
			bytes = num * multiplier;
			return true;
		}

		return false;
	}
}
