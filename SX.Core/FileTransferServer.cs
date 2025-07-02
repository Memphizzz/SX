using Newtonsoft.Json;
using Spectre.Console;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SX.Core;

public class FileTransferServer(Configuration config)
{
	private CancellationTokenSource _cancellationTokenSource;
	private TcpListener _listener;
	private Task _listenerTask;
	private static ProgressTask _currentProgressTask;

	public async Task StartAsync(CancellationToken cancellationToken = default)
	{
		_cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		config.EnsureDownloadDirectoryExists();

		_listener = new TcpListener(IPAddress.Loopback, config.Port);
		_listener.Start();

		AnsiConsole.MarkupLine($"[green]🎧 SX File Transfer Server listening on port {config.Port}[/]");
		_listenerTask = Task.Run(() => ListenForConnections(_cancellationTokenSource.Token), cancellationToken);
		await _listenerTask;
	}

	public async Task StopAsync()
	{
		try
		{
			// Cancel the operations first
			if (_cancellationTokenSource != null)
				await _cancellationTokenSource.CancelAsync();

			// Stop the listener to unblock AcceptTcpClientAsync
			_listener?.Stop();

			// Wait for the listener task to complete with a reasonable timeout
			if (_listenerTask != null)
				await _listenerTask.WaitAsync(TimeSpan.FromSeconds(2));
		}
		catch (TimeoutException)
		{
			Console.WriteLine("Server shutdown timed out, forcing exit");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error during shutdown: {ex.Message}");
		}
		finally
		{
			_cancellationTokenSource?.Dispose();
			_listener = null;
			_listenerTask = null;
		}
	}

	private async Task ListenForConnections(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
			try
			{
				var tcpClient = await _listener!.AcceptTcpClientAsync(cancellationToken);
				_ = Task.Run(() => HandleClient(tcpClient, cancellationToken), cancellationToken);
			}
			catch (Exception ex)
			{
				switch (ex)
				{
					case ObjectDisposedException:
						// Listener was stopped, exit gracefully
						break;

					case OperationCanceledException:
						// Cancellation was requested, exit gracefully
						break;

					default:
						if (!cancellationToken.IsCancellationRequested)
							AnsiConsole.MarkupLine($"[red]❌ Error accepting connection: {ex.Message}[/]");
						break;
				}
			}
	}

	private async Task HandleClient(TcpClient client, CancellationToken cancellationToken)
	{
		await ExecuteServerOperation(async () =>
		{
			using (client)
			await using (var stream = client.GetStream())
			{
				AnsiConsole.MarkupLine($"[cyan]🔗 Client connected from {client.Client.RemoteEndPoint}[/]");

				var protocol = await ParseProtocolMessage(stream);
				if (protocol == null)
				{
					AnsiConsole.MarkupLine("[red]❌ Invalid protocol headers[/]");
					return;
				}

				switch (protocol.Command)
				{
					case ProtocolCommand.ListDir:
						// Handle directory listing (sxls - list available files)
						await HandleDirectoryListing(stream, protocol, cancellationToken);
						break;

					case ProtocolCommand.Request:
						// Handle file request (sxu - send file to remote)
						await HandleFileRequest(stream, protocol, cancellationToken);
						break;

					case ProtocolCommand.Send:
					{
						// Handle file receive (sxd - receive file from remote)
						if (protocol.Size == null)
						{
							AnsiConsole.MarkupLine("[red]❌ No file size specified[/]");
							return;
						}

						if (protocol.Size > config.MaxFileSize)
						{
							AnsiConsole.MarkupLine($"[red]❌ File too large: {ProgressUtility.FormatBytes(protocol.Size.Value)} (max: {ProgressUtility.FormatBytes(config.MaxFileSize)})[/]");
							return;
						}

						var filePath = config.GetDownloadPath(protocol.Filename ?? "unknown");
						var tempPath = filePath + ".tmp";

						AnsiConsole.MarkupLine($"[blue]📥 Receiving file: {protocol.Filename} ({ProgressUtility.FormatBytes(protocol.Size.Value)})[/]");
						AnsiConsole.MarkupLine($"[dim]💾 Saving to: {filePath}[/]");

						try
						{
							// Receive file to temp location first
							await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write);

							if (protocol.Size.Value > 100 * 1024) // Show progress for files > 100KB
							{
								var startTime = DateTime.UtcNow;
								await AnsiConsole.Progress()
												 .Columns(Configuration.GetProgressColumns())
												 .StartAsync(async ctx =>
												 {
													 var task = ctx.AddTask($"[green]Receiving {Path.GetFileName(filePath)}[/]");
													 task.MaxValue = protocol.Size.Value;

													 await ReceiveFile(stream, fileStream, protocol.Size.Value, task, startTime, cancellationToken);
												 });
							}
							else
							{
								// Small file - no progress bar
								await ReceiveFile(stream, fileStream, protocol.Size.Value, null, DateTime.UtcNow, cancellationToken);
							}

							// Close the file stream before moving
							fileStream.Close();

							// Move temp file to final location
							if (File.Exists(filePath))
							{
								if (config.AllowOverwrite)
									File.Delete(filePath);
								else
								{
									File.Delete(tempPath);
									throw new InvalidOperationException($"File already exists: {protocol.Filename}");
								}
							}

							File.Move(tempPath, filePath);
							AnsiConsole.MarkupLine($"[green]✅ File received successfully: {protocol.Filename}[/]");
						}
						catch
                            {
                                // Clean up temp file on error
                                if (File.Exists(tempPath))
									try { File.Delete(tempPath); } catch { /* ignored */ }
								throw;
                            }
                            break;
                        }
                }
			}
		}, "Error handling client", cancellationToken);
	}

	private async Task<ProtocolMessage> ParseProtocolMessage(NetworkStream stream)
	{
		try
		{
			// Read JSON message line
			var jsonLine = await ReadLineAsync(stream);
			if (string.IsNullOrEmpty(jsonLine))
				return null;

			var protocol = JsonConvert.DeserializeObject<ProtocolMessage>(jsonLine);
			if (protocol == null)
				return null;

			// For Send command, we expect a DATA: line after the JSON
			if (protocol.Command == ProtocolCommand.Send)
			{
				var dataLine = await ReadLineAsync(stream);
				if (dataLine != ProtocolConstants.Data)
					return null;
			}

			return protocol;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private async Task<string> ReadLineAsync(NetworkStream stream)
	{
		var line = new List<byte>();

		while (true)
		{
			var buffer = new byte[1];
			var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1));
			if (bytesRead == 0)
				return null;

			if (buffer[0] == '\n')
				return Encoding.UTF8.GetString(line.ToArray()).Trim();

			if (buffer[0] != '\r') // Skip \r characters
				line.Add(buffer[0]);
		}
	}

	private async Task HandleFileRequest(NetworkStream stream, ProtocolMessage protocol, CancellationToken cancellationToken)
	{
		await ExecuteServerOperation(async () =>
		{
			AnsiConsole.MarkupLine($"[blue]📤 File requested: {protocol.Path}[/]");

			var fullPath = config.GetServeFilePath(protocol.Path ?? "");
			AnsiConsole.MarkupLine($"[dim]🔍 Resolved path: {fullPath}[/]");

			if (!File.Exists(fullPath))
			{
				AnsiConsole.MarkupLine($"[red]❌ File not found: {fullPath}[/]");
				await SendErrorResponse(stream, $"File not found: {protocol.Path}", cancellationToken);
				return;
			}

			var fileInfo = new FileInfo(fullPath);
			if (fileInfo.Length > config.MaxFileSize)
			{
				AnsiConsole.MarkupLine($"[red]❌ File too large: {ProgressUtility.FormatBytes(fileInfo.Length)} (max: {ProgressUtility.FormatBytes(config.MaxFileSize)})[/]");
				await SendErrorResponse(stream, $"File too large: {ProgressUtility.FormatBytes(fileInfo.Length)} (max: {ProgressUtility.FormatBytes(config.MaxFileSize)})", cancellationToken);
				return;
			}

            AnsiConsole.MarkupLine($"[green]📤 Sending file: {protocol.Path} ({ProgressUtility.FormatBytes(fileInfo.Length)})[/]");

            // Send JSON response
            var response = new ProtocolMessage
            {
                Command = ProtocolCommand.Send,
                Filename = Path.GetFileName(protocol.Path),
                Size = fileInfo.Length
            };
            var jsonResponse = JsonConvert.SerializeObject(response);
            var jsonBytes = Encoding.UTF8.GetBytes(jsonResponse + "\n");
            await stream.WriteAsync(jsonBytes, cancellationToken);

			// Send DATA separator
			var dataBytes = Encoding.UTF8.GetBytes(ProtocolConstants.Data + "\n");
			await stream.WriteAsync(dataBytes, cancellationToken);

			// Send file data with progress
			await using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);

			if (fileInfo.Length > 100 * 1024) // Show progress for files > 100KB
			{
				var startTime = DateTime.UtcNow;
				await AnsiConsole.Progress()
								 .Columns(Configuration.GetProgressColumns())
								 .StartAsync(async ctx =>
								 {
									 var task = ctx.AddTask($"[green]Sending {Path.GetFileName(fullPath)}[/]");
									 task.MaxValue = fileInfo.Length;

									 await SendFile(fileStream, stream, fileInfo.Length, task, startTime, cancellationToken);
								 });
			}
			else
			{
				// Small file - no progress bar
				await SendFile(fileStream, stream, fileInfo.Length, null, DateTime.UtcNow, cancellationToken);
			}

			AnsiConsole.MarkupLine($"[green]✅ File sent successfully: {protocol.Path}[/]");
		}, "Error sending file", cancellationToken);
	}

	private async Task HandleDirectoryListing(NetworkStream stream, ProtocolMessage protocol, CancellationToken cancellationToken)
	{
		await ExecuteServerOperation(async () =>
		{
			AnsiConsole.MarkupLine($"[blue]📂 Directory listing requested: {protocol.Path}[/]");

			var entries = config.ListServeFiles(protocol.Path ?? "");
			var listing = new DirectoryListing { Entries = entries };
			var jsonResponse = JsonConvert.SerializeObject(listing);

			AnsiConsole.MarkupLine($"[green]📋 Sending directory listing ({entries.Count} items)[/]");

			// Send JSON response
			var responseBytes = Encoding.UTF8.GetBytes(jsonResponse + "\n");
			await stream.WriteAsync(responseBytes, cancellationToken);

			AnsiConsole.MarkupLine("[green]✅ Directory listing sent successfully[/]");
		}, "Error sending directory listing", cancellationToken);
	}

	private async Task SendFile(FileStream fileStream, NetworkStream stream, long fileSize, ProgressTask progressTask, DateTime startTime, CancellationToken cancellationToken)
	{
		_currentProgressTask = progressTask;
		var buffer = new byte[8192];
		long totalSent = 0;

		while (totalSent < fileSize && !cancellationToken.IsCancellationRequested)
		{
			var bytesToRead = (int)Math.Min(buffer.Length, fileSize - totalSent);
			var bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, bytesToRead), cancellationToken);

			if (bytesRead == 0)
				break;

			// Use a timeout for write operations to detect hanging connections
			using var writeTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, writeTimeoutCts.Token);

			await stream.WriteAsync(buffer.AsMemory(0, bytesRead), linkedCts.Token);
			await stream.FlushAsync(linkedCts.Token);

			totalSent += bytesRead;
			progressTask?.Increment(bytesRead);

			// Update description with transfer speed for progress bar if enabled
			if (progressTask != null)
				ProgressUtility.UpdateProgress(progressTask, totalSent, startTime, Path.GetFileName(fileStream.Name), "Sending");
		}

		_currentProgressTask = null;
	}

	private async Task ReceiveFile(NetworkStream stream, FileStream fileStream, long expectedSize, ProgressTask progressTask, DateTime startTime, CancellationToken cancellationToken)
	{
		_currentProgressTask = progressTask;
		var buffer = new byte[8192];
		long totalReceived = 0;

		while (totalReceived < expectedSize && !cancellationToken.IsCancellationRequested)
		{
			var bytesToRead = (int)Math.Min(buffer.Length, expectedSize - totalReceived);
			var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, bytesToRead), cancellationToken);

			if (bytesRead == 0)
				break;

			await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
			totalReceived += bytesRead;

			progressTask?.Increment(bytesRead);

			// Update description with transfer speed for progress bar if enabled
			if (progressTask != null)
				ProgressUtility.UpdateProgress(progressTask, totalReceived, startTime, Path.GetFileName(fileStream.Name), "Receiving");
		}

		// Check if transfer was incomplete
		if (totalReceived < expectedSize)
			throw new TransferIncompleteException(totalReceived, expectedSize);

		_currentProgressTask = null;
	}

	private static async Task SendErrorResponse(NetworkStream stream, string errorMessage, CancellationToken cancellationToken)
	{
		var errorResponse = new ProtocolMessage
		{
			Command = ProtocolCommand.Error,
			ErrorMessage = errorMessage
		};
		var jsonResponse = JsonConvert.SerializeObject(errorResponse);
		var jsonBytes = Encoding.UTF8.GetBytes(jsonResponse + "\n");
		await stream.WriteAsync(jsonBytes, cancellationToken);
	}

	private static async Task ExecuteServerOperation(Func<Task> operation, string operationName, CancellationToken cancellationToken = default)
	{
		try
		{
			await operation();
		}
		catch (Exception ex)
		{
			// Stop any active progress task
			_currentProgressTask?.StopTask();
			_currentProgressTask = null;

			switch (ex)
			{
				case OperationCanceledException:
					// Timeout during write - client stopped responding
					AnsiConsole.MarkupLine("[red]❌ Upload interrupted - client stopped responding[/]");
					break;

				case TransferIncompleteException tie:
					// Client disconnected during receive
					AnsiConsole.MarkupLine($"[red]❌ Download interrupted - connection lost ({ProgressUtility.FormatBytes(tie.BytesReceived)} of {ProgressUtility.FormatBytes(tie.ExpectedBytes)} received)[/]");
					break;

				case SocketException:
				case IOException { InnerException: SocketException }:
					// Direct socket errors
					AnsiConsole.MarkupLine("[red]❌ Connection lost - network error[/]");
					break;

				default:
					AnsiConsole.MarkupLine($"[red]❌ {ex.Message}[/]");
					break;
			}
		}
	}
}
