using Spectre.Console;
using SX.Core;

namespace SX.Client;

public static class ShellIntegration
{
	public static readonly string DefaultPort = "53690";

	public static string GenerateBashCompletion() =>
		"""
		#!/bin/bash
		# SX Shell Completion for Bash
		# Source this file once: source ~/.sx/sx_completion.bash
		# Add to ~/.bashrc: source ~/.sx/sx_completion.bash

		_sx_complete() {
		    local cur="${COMP_WORDS[COMP_CWORD]}"
		    local prev="${COMP_WORDS[COMP_CWORD-1]}"
		    local cache_dir="$HOME/.sx"
		    
		    case "$prev" in
		        sxd)
		            # Download - complete with files only
		            if [[ -f "$cache_dir/files.cache" ]]; then
		                local files=$(cat "$cache_dir/files.cache" 2>/dev/null | tr '\n' ' ')
		                COMPREPLY=($(compgen -W "$files" -- "$cur"))
		            fi
		            return 0
		            ;;
		        sxls)
		            # List - complete with directories only
		            if [[ -f "$cache_dir/dirs.cache" ]]; then
		                local dirs=$(cat "$cache_dir/dirs.cache" 2>/dev/null | tr '\n' ' ')
		                COMPREPLY=($(compgen -W "$dirs" -- "$cur"))
		            fi
		            return 0
		            ;;
		    esac
		}

		complete -F _sx_complete sxd sxls

		""";

	public static string GenerateFishCompletion() =>
		"""
		# SX Shell Completion for Fish
		# Source this file once: source ~/.sx/sx_completion.fish
		# Add to ~/.config/fish/config.fish: source ~/.sx/sx_completion.fish

		# Function to read cache files
		function __sx_files
		    if test -f ~/.sx/files.cache
		        cat ~/.sx/files.cache 2>/dev/null
		    end
		end

		function __sx_dirs
		    if test -f ~/.sx/dirs.cache
		        cat ~/.sx/dirs.cache 2>/dev/null
		    end
		end

		# Clear existing completions
		complete -c sxd -e
		complete -c sxls -e

		# Dynamic completions that read from cache
		complete -c sxd -f -a '(__sx_files)' -d 'Download file from server'
		complete -c sxls -f -a '(__sx_dirs)' -d 'List server directory'

		""";

	public static async Task GenerateCompletionCache(List<DirectoryEntry> entries, string currentPath)
	{
		try
		{
			var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			var cacheDir = Path.Combine(homeDir, ".sx");

			// Ensure cache directory exists
			Directory.CreateDirectory(cacheDir);

			// Build file paths for current directory
			var files = new List<string>();
			var dirs = new List<string>();

			foreach (var entry in entries)
			{
				var fullPath = string.IsNullOrEmpty(currentPath) ? entry.Name : $"{currentPath}/{entry.Name}";

				if (entry.Type == EntryType.File)
					files.Add(fullPath);
				else
					dirs.Add(fullPath);
			}

			// Write cache files that completion scripts will read
			await File.WriteAllLinesAsync(Path.Combine(cacheDir, "files.cache"), files);
			await File.WriteAllLinesAsync(Path.Combine(cacheDir, "dirs.cache"), dirs);

			// Generate completion scripts (only once, they read from cache)
			var bashCompletionFile = Path.Combine(cacheDir, "sx_completion.bash");
			var fishCompletionFile = Path.Combine(cacheDir, "sx_completion.fish");

			if (!File.Exists(bashCompletionFile))
			{
				await File.WriteAllTextAsync(bashCompletionFile, GenerateBashCompletion());
				AnsiConsole.MarkupLine("[dim]✅ Generated bash completion script in ~/.sx/sx_completion.bash[/]");
				AnsiConsole.MarkupLine("[dim]   Add to ~/.bashrc: source ~/.sx/sx_completion.bash[/]");
			}

			if (!File.Exists(fishCompletionFile))
			{
				await File.WriteAllTextAsync(fishCompletionFile, GenerateFishCompletion());
				AnsiConsole.MarkupLine("[dim]✅ Generated fish completion script in ~/.sx/sx_completion.fish[/]");
				AnsiConsole.MarkupLine("[dim]   Add to ~/.config/fish/config.fish: source ~/.sx/sx_completion.fish[/]");
			}

			AnsiConsole.MarkupLine("[dim]📝 Updated completion cache[/]");
		}
		catch (Exception ex)
		{
			AnsiConsole.MarkupLine($"[yellow]⚠️  Could not generate completion cache: {ex.Message}[/]");
		}
	}

	public static void ShowHelp()
	{
		AnsiConsole.MarkupLine("[yellow]SX - SSH File Transfer System[/]");
		AnsiConsole.MarkupLine("[yellow]=============================[/]");
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[white]Available commands:[/]");
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[blue]📂 sxls [[path]][/]           - List files in serve directory");
		AnsiConsole.MarkupLine("[blue]📥 sxd <path> [[name]][/]     - Download: Get file from local server to here");
		AnsiConsole.MarkupLine("[blue]📤 sxu <file>[/]            - Upload: Send file from here to local server");
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[white]Examples:[/]");
		AnsiConsole.MarkupLine("  [dim]sxls[/]                   # List root directory");
		AnsiConsole.MarkupLine("  [dim]sxls subdir[/]           # List subdirectory");
		AnsiConsole.MarkupLine("  [dim]sxd 1GB.bin[/]           # Download file from local server");
		AnsiConsole.MarkupLine("  [dim]sxd dir/file.txt local.txt[/]  # Download file, rename locally");
		AnsiConsole.MarkupLine("  [dim]sxu myfile.txt[/]        # Upload remote file to local server");
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[white]Environment:[/]");
		var currentPort = Environment.GetEnvironmentVariable("SX_PORT") ?? DefaultPort;
		AnsiConsole.MarkupLine($"  [dim]SX_PORT={currentPort}[/]  # Port to connect to");
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[white]Setup:[/]");
		AnsiConsole.MarkupLine("  [dim]1. Start local server: dotnet run --project SX.CLI -- --dir /path/to/files[/]");
		AnsiConsole.MarkupLine("  [dim]2. SSH with tunnel:    ssh -R 53690:localhost:53690 user@server[/]");
		AnsiConsole.MarkupLine("  [dim]3. Use commands above on remote server[/]");
	}
}
