using Newtonsoft.Json;
using Spectre.Console;
using SX.Core;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SX.Client;

public class Program
{
	private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(3);
	private static readonly TimeSpan DataTimeout = TimeSpan.FromSeconds(1);

	public static async Task<int> Main(string[] args)
	{
		// Configure console encoding and Spectre.Console for emoji support
		Console.OutputEncoding = Encoding.UTF8;
		AnsiConsole.Profile.Capabilities.Unicode = true;
		AnsiConsole.Profile.Capabilities.Ansi = true;
		AnsiConsole.Profile.Capabilities.ColorSystem = ColorSystem.TrueColor;

		// Set up cancellation for Ctrl+C
		using var cts = new CancellationTokenSource();
		Console.CancelKeyPress += (_, e) =>
		{
			e.Cancel = true;
			AnsiConsole.MarkupLine("\n[yellow]⏹️  Cancelling operation...[/]");
			cts.Cancel();
		};

		try
		{
			if (args.Length == 0)
			{
				ShellIntegration.ShowHelp();
				return 0;
			}

			// Always expect subcommands with sx tool
			if (args.Length == 0 || args[0] == "help" || args[0] == "--help" || args[0] == "-h")
			{
				ShellIntegration.ShowHelp();
				return 0;
			}

			var command = args[0];
			args = args.Skip(1).ToArray();

			var port = Environment.GetEnvironmentVariable("SX_PORT") ?? ShellIntegration.DefaultPort;

			return command.ToLower() switch
			{
				"sxd"  => await HandleDownload(args, port, cts.Token),
				"sxu"  => await HandleUpload(args, port, cts.Token),
				"sxls" => await HandleList(args, port, cts.Token),
				var _  => HandleUnknownCommand(command)
			};
		}
		catch (OperationCanceledException)
		{
			AnsiConsole.MarkupLine("[yellow]✅ Operation cancelled by user.[/]");
			return 130; // Standard exit code for Ctrl+C
		}
		catch (Exception ex)
		{
			AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
			return 1;
		}
	}

	private static async Task<int> HandleDownload(string[] args, string port, CancellationToken cancellationToken)
	{
		if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
		{
			AnsiConsole.MarkupLine("[yellow]sxd - Download files from SX server[/]");
			AnsiConsole.MarkupLine("[white]Usage: sxd <remote_path> [[local_name]][/]");
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine("[white]Arguments:[/]");
			AnsiConsole.MarkupLine("  [blue]remote_path[/]  - Path to file on server");
			AnsiConsole.MarkupLine("  [blue]local_name[/]   - Local filename (optional)");
			return args.Length == 0 ? 1 : 0;
		}

		var remotePath = args[0];
		var localName = args.Length > 1 ? args[1] : Path.GetFileName(remotePath);

		return await ExecuteClientOperation(() => DownloadFile(remotePath, localName, port, cancellationToken),
											port,
											"Download failed");
	}

	private static async Task<int> HandleUpload(string[] args, string port, CancellationToken cancellationToken)
	{
		if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
		{
			AnsiConsole.MarkupLine("[yellow]sxu - Upload files to SX server[/]");
			AnsiConsole.MarkupLine("[white]Usage: sxu <local_file>[/]");
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine("[white]Arguments:[/]");
			AnsiConsole.MarkupLine("  [blue]local_file[/]  - Path to local file to upload");
			return args.Length == 0 ? 1 : 0;
		}

		var localFile = args[0];

		if (!File.Exists(localFile))
		{
			AnsiConsole.MarkupLine($"[red]File not found: {localFile}[/]");
			return 1;
		}

		return await ExecuteClientOperation(() => UploadFile(localFile, port, cancellationToken),
											port,
											"Upload failed");
	}

	private static async Task<int> HandleList(string[] args, string port, CancellationToken cancellationToken)
	{
		if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h"))
		{
			AnsiConsole.MarkupLine("[yellow]sxls - List files on SX server[/]");
			AnsiConsole.MarkupLine("[white]Usage: sxls [[remote_path]][/]");
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine("[white]Arguments:[/]");
			AnsiConsole.MarkupLine("  [blue]remote_path[/]  - Directory path on server (optional, defaults to root)");
			return 0;
		}

		var remotePath = args.Length > 0 ? args[0] : "";
		return await ExecuteClientOperation(() => ListFiles(remotePath, port, cancellationToken),
											port,
											"Directory listing failed");
	}

	private static async Task<int> DownloadFile(string remotePath, string localName, string port, CancellationToken cancellationToken)
	{
		using var client = new TcpClient();

		await client.ConnectAsync(IPAddress.Loopback, int.Parse(port), cancellationToken);

		// Note: client.Connected is not reliable for SSH port forwarded connections
		// The TCP connection succeeds to the SSH tunnel even when the target server isn't running
		// We detect actual connection failure when no response is received

		await using var stream = client.GetStream();

        // Send JSON request
        var request = new ProtocolMessage
        {
            Command = ProtocolCommand.Request,
            Path = remotePath
        };
        var requestJson = JsonConvert.SerializeObject(request) + "\n";
        var requestBytes = Encoding.UTF8.GetBytes(requestJson);
        
		await stream.WriteAsync(requestBytes, cancellationToken);

		// Parse JSON response with timeout
		var responseLine = await ReadLineWithTimeoutAsync(stream, ResponseTimeout, cancellationToken);

		if (string.IsNullOrEmpty(responseLine))
		{
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine($"[red]❌ Failed to establish connection to server on port {port}[/]");
			return 1;
		}

		var response = JsonConvert.DeserializeObject<ProtocolMessage>(responseLine);
		if (response == null)
		{
			AnsiConsole.MarkupLine("[red]❌ Invalid server response[/]");
			return 1;
		}

		// Check for error response
		if (response.Command == ProtocolCommand.Error)
		{
			AnsiConsole.MarkupLine($"[red]❌ {response.ErrorMessage}[/]");
			return 1;
		}

		if (response.Size == null)
		{
			AnsiConsole.MarkupLine("[red]❌ Invalid server response - no size information[/]");
			return 1;
		}

		var fileSize = response.Size.Value;

		// Read DATA separator
		var dataLine = await ReadLineWithTimeoutAsync(stream, DataTimeout, cancellationToken);
		if (dataLine != "DATA:")
		{
			AnsiConsole.MarkupLine("[red]❌ Expected DATA separator[/]");
			return 1;
		}

		AnsiConsole.MarkupLine($"[blue]📥 Requesting Download of: {remotePath}[/]");

		var tempPath = localName + ".tmp";

		try
		{
			await using var fileStream = File.Create(tempPath);

			if (fileSize > 100 * 1024) // Show progress for files > 100KB
			{
				var startTime = DateTime.UtcNow;
				await AnsiConsole.Progress()
								 .Columns(Configuration.GetProgressColumns())
								 .StartAsync(async ctx =>
								 {
									 var task = ctx.AddTask($"[green]Downloading {localName}[/]");
									 task.MaxValue = fileSize;

									 await Download(stream, fileStream, fileSize, task, startTime, cancellationToken);
								 });
			}
			else
			{
				// Small file - no progress bar
				await Download(stream, fileStream, fileSize, null, null, cancellationToken);
			}

			// Close the file stream before moving
			fileStream.Close();

			// Move temp file to final location
			if (File.Exists(localName))
				File.Delete(localName);

			File.Move(tempPath, localName);

			AnsiConsole.MarkupLine($"[green]📊 Size: {ProgressUtility.FormatBytes(fileSize)}[/]");
			AnsiConsole.MarkupLine($"[green]✅ Download completed successfully! File saved as: {localName}[/]");
			return 0;
		}
		catch
        {
            // Clean up temp file on error
            if (File.Exists(tempPath))
				try { File.Delete(tempPath); } catch { /* ignored */ }
			throw;
        }
    }

	private static async Task<int> UploadFile(string localFile, string port, CancellationToken cancellationToken)
	{
		var fileInfo = new FileInfo(localFile);
		var fileName = fileInfo.Name;

		using var client = new TcpClient();

		await client.ConnectAsync(IPAddress.Loopback, int.Parse(port), cancellationToken);

		// Note: client.Connected is not reliable for SSH port forwarded connections
		// The TCP connection succeeds to the SSH tunnel even when the target server isn't running
		// We detect actual connection failure when no response is received

		await using var stream = client.GetStream();

        // Send JSON headers
        var message = new ProtocolMessage
        {
            Command = ProtocolCommand.Send,
            Filename = fileName,
            Size = fileInfo.Length
        };
        var messageJson = JsonConvert.SerializeObject(message) + "\n";
        var messageBytes = Encoding.UTF8.GetBytes(messageJson);
        await stream.WriteAsync(messageBytes, cancellationToken);

		// Send DATA separator
		var dataBytes = Encoding.UTF8.GetBytes("DATA:\n");
		await stream.WriteAsync(dataBytes, cancellationToken);

		AnsiConsole.MarkupLine($"[blue]📤 Requesting Upload of: {fileName}[/]");
		AnsiConsole.MarkupLine($"[blue]📊 Size: {ProgressUtility.FormatBytes(fileInfo.Length)}[/]");

		// Upload file with progress bar
		await using var fileStream = File.OpenRead(localFile);

		if (fileInfo.Length > 100 * 1024) // Show progress for files > 100KB
		{
			var startTime = DateTime.UtcNow;
			await AnsiConsole.Progress()
							 .Columns(Configuration.GetProgressColumns())
							 .StartAsync(async ctx =>
							 {
								 var task = ctx.AddTask($"[green]Uploading {fileName}[/]");
								 task.MaxValue = fileInfo.Length;

								 await Upload(fileStream, stream, fileInfo.Length, task, startTime, cancellationToken);
							 });
		}
		else
		{
			// Small file - no progress bar
			await Upload(fileStream, stream, fileInfo.Length, null, null, cancellationToken);
		}

		AnsiConsole.MarkupLine("[green]✅ Upload completed successfully![/]");
		return 0;
	}

	private static async Task<int> ListFiles(string remotePath, string port, CancellationToken cancellationToken)
	{
		using var client = new TcpClient();

		await client.ConnectAsync(IPAddress.Loopback, int.Parse(port), cancellationToken);

		// Note: client.Connected is not reliable for SSH port forwarded connections
		// The TCP connection succeeds to the SSH tunnel even when the target server isn't running
		// We detect actual connection failure when no response is received

		await using var stream = client.GetStream();

        // Send JSON request
        var request = new ProtocolMessage
        {
            Command = ProtocolCommand.ListDir,
            Path = remotePath
        };
        var requestJson = JsonConvert.SerializeObject(request) + "\n";
        var requestBytes = Encoding.UTF8.GetBytes(requestJson);
        await stream.WriteAsync(requestBytes, cancellationToken);

		// Read JSON response with timeout
		var responseLine = await ReadLineWithTimeoutAsync(stream, ResponseTimeout, cancellationToken);

		if (string.IsNullOrEmpty(responseLine))
		{
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine($"[red]❌ Failed to establish connection to server on port {port}[/]");
			return 1;
		}

		var listing = JsonConvert.DeserializeObject<DirectoryListing>(responseLine);
		if (listing?.Entries == null)
		{
			AnsiConsole.MarkupLine("[red]❌ Invalid directory listing response[/]");
			return 1;
		}

		AnsiConsole.MarkupLine($"[blue]📂 Listing files in directory: /{remotePath}[/]");
		AnsiConsole.WriteLine();

		if (listing.Entries.Count == 0)
			AnsiConsole.MarkupLine("[dim]No files or directories found[/]");
		else
		{
			var table = new Table();
			table.AddColumn("[yellow]Type[/]");
			table.AddColumn("[yellow]Name[/]");
			table.AddColumn("[yellow]Size[/]");
			table.AddColumn("[yellow]Modified[/]");

			table.Border = TableBorder.Rounded;
			table.BorderColor(Color.Grey);

			foreach (var entry in listing.Entries)
			{
				var modifyText = ProgressUtility.FormatRelativeDate(entry.ModifyDate);

				switch (entry.Type)
				{
					case EntryType.Dir:
						table.AddRow("[blue]📁 DIR[/]", $"[blue]{entry.Name}[/]", "[dim]-[/]", $"[dim]{modifyText}[/]");
						break;

					case EntryType.File:
						var sizeText = ProgressUtility.FormatBytes(entry.Size);
						table.AddRow("[white]📄 FILE[/]", $"[white]{entry.Name}[/]", $"[dim]{sizeText}[/]", $"[dim]{modifyText}[/]");
						break;

					default:
						throw new ArgumentOutOfRangeException();
				}
			}

			AnsiConsole.Write(table);
		}

		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[dim]💡 Use: sxd <path> [[local_name]] to download files[/]");
		AnsiConsole.MarkupLine("[dim]💡 Use: sxls <dir_path> to list subdirectories[/]");

		await ShellIntegration.GenerateCompletionCache(listing.Entries, remotePath);
		return 0;
	}

	private static async Task<string> ReadLineAsync(NetworkStream stream, CancellationToken cancellationToken = default)
	{
		var line = new List<byte>();

		while (true)
		{
			var buffer = new byte[1];
			var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
			if (bytesRead == 0)
				return null;

			if (buffer[0] == '\n')
				return Encoding.UTF8.GetString(line.ToArray()).Trim();
			if (buffer[0] != '\r') // Skip \r characters
				line.Add(buffer[0]);
		}
	}

	private static async Task<string> ReadLineWithTimeoutAsync(NetworkStream stream, TimeSpan timeout, CancellationToken cancellationToken = default)
	{
		using var timeoutCts = new CancellationTokenSource(timeout);
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		try
		{
			return await ReadLineAsync(stream, linkedCts.Token);
		}
		catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
		{
			// Timeout occurred
			return null;
		}
	}

	private static async Task Download(NetworkStream networkStream, FileStream fileStream, long totalSize, ProgressTask progressTask, DateTime? startTime = null, CancellationToken cancellationToken = default)
	{
		var buffer = new byte[8192];
		long totalReceived = 0;
		var actualStartTime = startTime ?? DateTime.UtcNow;

		while (totalReceived < totalSize)
		{
			var bytesToRead = (int)Math.Min(buffer.Length, totalSize - totalReceived);
			var bytesRead = await networkStream.ReadAsync(buffer.AsMemory(0, bytesToRead), cancellationToken);

			if (bytesRead == 0)
				break;

			await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
			totalReceived += bytesRead;

			progressTask?.Increment(bytesRead);

			// Update description with transfer speed if enabled
			if (progressTask != null)
				ProgressUtility.UpdateProgress(progressTask, totalReceived, actualStartTime, Path.GetFileName(fileStream.Name), "Downloading");
		}
	}

	private static async Task Upload(FileStream fileStream, NetworkStream networkStream, long totalSize, ProgressTask progressTask, DateTime? startTime = null, CancellationToken cancellationToken = default)
	{
		var buffer = new byte[8192];
		long totalSent = 0;
		var actualStartTime = startTime ?? DateTime.UtcNow;

		while (totalSent < totalSize)
		{
			var bytesToRead = (int)Math.Min(buffer.Length, totalSize - totalSent);
			var bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, bytesToRead), cancellationToken);

			if (bytesRead == 0)
				break;

			await networkStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
			totalSent += bytesRead;

			progressTask?.Increment(bytesRead);

			if (progressTask != null)
				ProgressUtility.UpdateProgress(progressTask, totalSent, actualStartTime, Path.GetFileName(fileStream.Name), "Uploading");
		}
	}

	private static int HandleUnknownCommand(string command)
	{
		AnsiConsole.MarkupLine($"[red]Unknown command: {command}[/]");
		ShellIntegration.ShowHelp();
		return 1;
	}

	private static async Task<int> ExecuteClientOperation(Func<Task<int>> operation, string port, string operationName)
	{
		try
		{
			return await operation();
		}
		catch (Exception ex)
		{
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine();

			switch (ex)
			{
				case OperationCanceledException cancelEx:
					// Cancellation is handled at the Main level, rethrow it
					throw cancelEx;

				case SocketException sockEx:
					AnsiConsole.MarkupLine($"[red]❌ Cannot connect to server on port {port}: {sockEx.Message}[/]");
					return 1;

				case JsonException jsonEx:
					AnsiConsole.MarkupLine($"[red]❌ Invalid response from server: {jsonEx.Message}[/]");
					return 1;

				default:
					AnsiConsole.MarkupLine($"[red]❌ {operation}: {ex.Message}[/]");
					return 1;
			}
		}
	}
}
