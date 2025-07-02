using Spectre.Console;

namespace SX.Core;

public class Configuration
{
	public int Port { get; set; } = 53690;
	public string DownloadDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
	public string ServeDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
	public long MaxFileSize { get; set; } = 10L * 1024 * 1024 * 1024; // 10GB
	public bool AllowOverwrite { get; set; } = true;

	public static Configuration Default => new();

	public static ProgressColumn[] GetProgressColumns() =>
	[
		new TaskDescriptionColumn(),
		new ProgressBarColumn(),
		new PercentageColumn(),
		new RemainingTimeColumn(),
		new SpinnerColumn(Spinner.Known.Clock)
	];

	public void EnsureDownloadDirectoryExists()
	{
		if (!Directory.Exists(DownloadDirectory))
			Directory.CreateDirectory(DownloadDirectory);
	}

	public void EnsureServeDirectoryExists()
	{
		if (!Directory.Exists(ServeDirectory))
			Directory.CreateDirectory(ServeDirectory);
	}

	public string GetServeFilePath(string relativePath)
	{
		// Sanitize the relative path to prevent directory traversal
		var safePath = relativePath.Replace("..", "").Replace("\\", "/");
		safePath = safePath.TrimStart('/');

		// Convert forward slashes to system-appropriate path separators
		var systemPath = safePath.Replace('/', Path.DirectorySeparatorChar);

		// Combine with serve directory
		var fullPath = Path.Combine(ServeDirectory, systemPath);

		// Ensure the path is within the serve directory
		var fullServePath = Path.GetFullPath(ServeDirectory);
		var resolvedPath = Path.GetFullPath(fullPath);

		if (!resolvedPath.StartsWith(fullServePath))
			throw new UnauthorizedAccessException("Path traversal attempt detected");

		return resolvedPath;
	}

	public List<DirectoryEntry> ListServeFiles(string relativePath = "")
	{
		var searchPath = string.IsNullOrEmpty(relativePath) ? ServeDirectory : GetServeFilePath(relativePath);

		if (!Directory.Exists(searchPath))
			return [];

		var entries = new List<DirectoryEntry>();

		// Add directories first
		foreach (var dir in Directory.GetDirectories(searchPath))
		{
			var dirName = Path.GetFileName(dir);
			var dirInfo = new DirectoryInfo(dir);
			entries.Add(new DirectoryEntry { Type = EntryType.Dir, Name = dirName, ModifyDate = dirInfo.LastWriteTime });
		}

		// Add files
		foreach (var file in Directory.GetFiles(searchPath))
		{
			var fileName = Path.GetFileName(file);
			var fileInfo = new FileInfo(file);
			entries.Add(new DirectoryEntry { Type = EntryType.File, Name = fileName, Size = fileInfo.Length, ModifyDate = fileInfo.LastWriteTime });
		}

		return entries;
	}

	private string GetSafeFileName(string fileName)
	{
		// Sanitize filename to prevent path traversal
		var safeName = Path.GetFileName(fileName);
		if (string.IsNullOrWhiteSpace(safeName))
			throw new ArgumentException("Invalid filename");

		// Remove any remaining invalid characters
		return Path.GetInvalidFileNameChars().Aggregate(safeName, (current, c) => current.Replace(c, '_'));
	}

	public string GetDownloadPath(string fileName)
	{
		var safeFileName = GetSafeFileName(fileName);
		var fullPath = Path.Combine(DownloadDirectory, safeFileName);

		// Handle duplicates if overwrite is disabled
		if (!AllowOverwrite && File.Exists(fullPath))
		{
			var directory = Path.GetDirectoryName(fullPath)!;
			var nameWithoutExt = Path.GetFileNameWithoutExtension(safeFileName);
			var extension = Path.GetExtension(safeFileName);
			var counter = 1;

			do
			{
				var newName = $"{nameWithoutExt}_{counter}{extension}";
				fullPath = Path.Combine(directory, newName);
				counter++;
			} while (File.Exists(fullPath));
		}

		return fullPath;
	}
}
