using Spectre.Console;

namespace SX.Core;

public static class ProgressUtility
{
	public static void UpdateProgress(ProgressTask progressTask, long totalTransferred, DateTime startTime, string fileName, string operation)
	{
		var elapsed = DateTime.UtcNow - startTime;
		if (elapsed.TotalSeconds > 0.1)
		{
			var speed = totalTransferred / elapsed.TotalSeconds;
			var speedText = FormatBytesPerSecond(speed);
			progressTask.Description = $"[green]{operation} {fileName} ({speedText})[/]";
		}
	}

	public static string FormatBytes(long bytes)
	{
		string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
		var suffixIndex = 0;
		double size = bytes;

		while (size >= 1024 && suffixIndex < suffixes.Length - 1)
		{
			size /= 1024;
			suffixIndex++;
		}

		return $"{size:F1} {suffixes[suffixIndex]}";
	}

	public static string FormatBytesPerSecond(double bytesPerSecond)
	{
		string[] suffixes = ["B/s", "KB/s", "MB/s", "GB/s"];
		var suffixIndex = 0;
		var speed = bytesPerSecond;

		while (speed >= 1024 && suffixIndex < suffixes.Length - 1)
		{
			speed /= 1024;
			suffixIndex++;
		}

		return $"{speed:F1} {suffixes[suffixIndex]}";
	}

	public static string FormatRelativeDate(DateTime date)
	{
		var now = DateTime.Now;
		var timeSpan = now - date;

		return timeSpan.TotalDays switch
		{
			< 1 when timeSpan.TotalHours < 1 => timeSpan.TotalMinutes < 1 ? "just now" : $"{(int)timeSpan.TotalMinutes}m ago",
			< 1                              => $"{(int)timeSpan.TotalHours}h ago",
			< 2                              => "yesterday",
			< 7                              => $"{(int)timeSpan.TotalDays}d ago",
			< 30                             => $"{(int)(timeSpan.TotalDays / 7)}w ago",
			< 365                            => $"{(int)(timeSpan.TotalDays / 30)}mo ago",
			var _                            => date.ToString("yyyy-MM-dd")
		};
	}
}
