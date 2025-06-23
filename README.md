# SX - SSH File Transfer System

[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download)
[![NuGet Client](https://img.shields.io/nuget/v/SX.Client.svg?label=nuget%20client)](https://www.nuget.org/packages/SX.Client/)
[![NuGet Server](https://img.shields.io/nuget/v/SX.Server.svg?label=nuget%20server)](https://www.nuget.org/packages/SX.Server/)
[![Snap](https://img.shields.io/badge/snap-coming%20soon-orange.svg)](#snap-packages-coming-soon)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-purple.svg)](#)

**SX** (SSH eXchange) is a modern, cross-platform file transfer system that enables seamless file transfers between remote SSH sessions and local machines without requiring separate connections or re-authentication.

## ✨ Features

- **🚀 Blazing Fast** - Native .NET performance with progress bars
- **🔒 Secure** - Uses SSH reverse tunnels for encrypted transfers
- **🌐 Cross-Platform** - Works on Windows, Linux, and macOS
- **📊 Beautiful UI** - Rich console output with progress indicators and file tables
- **🎯 Simple** - Just three commands: `sxd` (download), `sxu` (upload), `sxls` (list)
- **⚡ Tab Completion** - Smart shell completion for remote file paths
- **📁 Directory Browsing** - Explore remote directories with file sizes and dates
- **🎨 No Dependencies** - Self-contained executables

## 🎯 How It Works
**Setup:** SX.Server runs on your local workstation, SX.Client runs on remote servers you SSH into.

**Workflow:** 
1. Start `sx-server` on your workstation
2. SSH with reverse tunnel: `ssh -R 53690:localhost:53690 user@server`  
3. From within your SSH session, use: `sxd filename` (download from workstation), `sxu filename` (upload to workstation), `sxls` (list workstation files)

## 🎬 Quick Demo
*Note: All commands below are run from within your SSH session on the remote server. From this perspective, "download" means getting files from your local workstation, and "upload" means sending files back to your workstation.*
```bash
# List files from your local workstation
$ sxls
┌──────┬─────────────────┬──────────┬──────────────┐
│ Type │ Name            │ Size     │ Modified     │
├──────┼─────────────────┼──────────┼──────────────┤
│ DIR  │ projects        │ -        │ 2h ago       │
│ FILE │ presentation.pdf│ 2.4 MB   │ yesterday    │
│ FILE │ data.csv        │ 156.7 KB │ 3d ago       │
└──────┴─────────────────┴──────────┴──────────────┘

# Download 'presentation.pdf' from your local workstation
$ sxd presentation.pdf
📥 Downloading: presentation.pdf (2.4 MB)
████████████████████████████████████████ 100% | 2.4 MB/s | 00:00:01

# Upload from remote machine to your workstation
$ sxu myfile.txt
📤 Uploading: myfile.txt (45.2 KB)
████████████████████████████████████████ 100% | 1.2 MB/s | 00:00:01
✅ Upload completed successfully!
```

## 🚀 Quick Start

### Prerequisites

SX requires .NET 9.0 or later. If you don't have it installed:

**Ubuntu/Debian:**
```bash
# Ubuntu 24.10
sudo apt update && sudo apt install dotnet-sdk-9.0

# Ubuntu 24.04, 22.04, 20.04 (requires backports PPA)
sudo add-apt-repository ppa:dotnet/backports
sudo apt update && sudo apt install dotnet-sdk-9.0
```

**Other Linux/macOS/Windows:**  
See [Microsoft's .NET 9 installation guide](https://learn.microsoft.com/en-us/dotnet/core/install/)

**Verify installation:**
```bash
dotnet --version  # Should show 9.x.x
```

### 1. Install SX

**Option A: Via .NET Tool**
```bash
# Install globally via .NET tool
# On your local workstation:
dotnet tool install -g SX.Server
# On remote servers:
dotnet tool install -g SX.Client
```

**Option B: Via Snap (Coming Soon!)**
```bash
# Snap packages are currently under review
# Will be available as: sx-client and sx-server
```

**Option C: Build from Source**
```bash
git clone https://github.com/Memphizzz/sx
cd sx
dotnet pack SX.Server --configuration Release --output ./packages
dotnet tool install --global --add-source ./packages SX.Server
```

### 2. Setup Convenient Commands

The setup scripts are included with the client package:

**For Bash/Zsh:**
```bash
# From the package
source ~/.dotnet/tools/.store/sx.client/1.x.x/sx.client/1.x.x/scripts/setup-sx-commands.sh
# Or download from GitHub if you prefer
curl -sL https://raw.githubusercontent.com/Memphizzz/SX/main/setup-sx-commands.sh | bash
```

**For Fish Shell:**
```bash
# From the package
source ~/.dotnet/tools/.store/sx.client/1.x.x/sx.client/1.x.x/scripts/setup-sx-fish.fish
# Or download from GitHub
curl -sL https://raw.githubusercontent.com/Memphizzz/SX/main/setup-sx-fish.fish | fish
```

**Or manually create aliases:**
```bash
# Add to your shell config (.bashrc, .zshrc, etc.)
alias sxd='~/.dotnet/tools/sx sxd'
alias sxu='~/.dotnet/tools/sx sxu'  
alias sxls='~/.dotnet/tools/sx sxls'
```

### 3. Start Local Server

```bash
# Start SX server to serve files from a directory
sx-server --dir ~/Downloads
```

### 4. Create SSH Tunnel

```bash
# Connect to remote server with reverse tunnel
ssh -R 53690:localhost:53690 user@remote-server
```

### 5. Use on Remote Server

```bash
# List files with beautiful table (generates completion cache)
sxls

# Enable tab completion (first time only, after running sxls)
source ~/.sx/sx_completion.bash  # or .fish for fish shell

# Download files (with tab completion!)
sxd <TAB>  # Shows available files
sxd largefile.zip

# Upload files (shell handles local completion)
sxu mylocal.txt
```

## 📖 Commands

| Command | Description | Example |
|---------|-------------|---------|
| `sxls [path]` | List files and directories | `sxls`, `sxls projects/` |
| `sxd <remote> [local]` | Download file from server | `sxd file.pdf`, `sxd data.csv backup.csv` |
| `sxu <local>` | Upload file to server | `sxu document.pdf` |

## 🎯 Tab Completion

SX includes intelligent shell completion that updates automatically:

1. **Run `sxls`** - Updates completion cache with current server files
2. **Press TAB** - Get smart completions:
   - `sxd <TAB>` - Complete with downloadable files
   - `sxls <TAB>` - Complete with directories

**Setup completion (one-time):**
```bash
# Bash
echo "source ~/.sx/sx_completion.bash" >> ~/.bashrc

# Fish  
echo "source ~/.sx/sx_completion.fish" >> ~/.config/fish/config.fish
```

## ⚙️ Configuration

### Server Options

```bash
sx-server [options]

Options:
  -p, --port <port>      Port to listen on (default: 53690)
  -d, --dir <path>       Directory to serve (default: ~/Downloads)  
      --max-size <size>  Maximum file size (default: 10GB)
      --no-overwrite     Don't overwrite existing files
  -h, --help             Show help
```

### Examples

```bash
# Custom server port and directory
sx-server --port 9999 --dir /data/shared

# With size limits
sx-server --max-size 1GB --no-overwrite

# Client using custom port
export SX_PORT=9999
sxd file.txt  # Client connects to port 9999
```

## 🏗️ Architecture

```
┌─────────────────┐    SSH Tunnel    ┌─────────────────┐
│  Remote Server  │◄─────────────────┤  Local Machine  │
│                 │                  │                 │
│  sxd/sxu/sxls   │     Port 53690   │    SX.Server    │
│   (SX.Client)   │                  │                 │
└─────────────────┘                  └─────────────────┘
```

- **SX.Core** - Core library with protocol, file handling, and utilities
- **SX.Server** - Server executable (local machine)
- **SX.Client** - Client commands (remote machine via SSH)
- **Protocol** - JSON-based communication over TCP

## 🛠️ Development

### Build

```bash
dotnet build
dotnet build --configuration Release
```

### Local Development

```bash
# Start server
dotnet run --project SX.Server -- --dir ./test-files

# Test client (in another terminal)
dotnet run --project SX.Client -- sxls
dotnet run --project SX.Client -- sxd testfile.txt
```

## 🔧 Troubleshooting

### Connection refused
1. Check if SX server is running locally
2. Verify SSH tunnel: `ssh -R 53690:localhost:53690 user@server`  
3. Check firewall settings
4. Try different port if conflicts occur

### Port conflicts
```bash
# Use different port
dotnet run --project SX.Server -- --port 9999
export SX_PORT=9999
ssh -R 9999:localhost:9999 user@server
```

### Completion not working
```bash
# Regenerate completion
rm -rf ~/.sx/
sxls  # Regenerates completion files

# Re-source completion
source ~/.sx/sx_completion.bash
```

## 📜 Manual Installation

If you prefer manual setup:

**Create wrapper scripts:**
```bash
mkdir -p ~/.local/bin

# Download command
echo '#!/bin/bash
exec ~/.dotnet/tools/sx sxd "$@"' > ~/.local/bin/sxd
chmod +x ~/.local/bin/sxd

# Upload command  
echo '#!/bin/bash
exec ~/.dotnet/tools/sx sxu "$@"' > ~/.local/bin/sxu
chmod +x ~/.local/bin/sxu

# List command
echo '#!/bin/bash
exec ~/.dotnet/tools/sx sxls "$@"' > ~/.local/bin/sxls
chmod +x ~/.local/bin/sxls

# Add to PATH
export PATH="$HOME/.local/bin:$PATH"
```

**Fish shell functions:**
```fish
mkdir -p ~/.config/fish/functions

echo 'function sxd --description "SX Download - Get file from server"
    ~/.dotnet/tools/sx sxd $argv
end' > ~/.config/fish/functions/sxd.fish

echo 'function sxu --description "SX Upload - Send file to server"  
    ~/.dotnet/tools/sx sxu $argv
end' > ~/.config/fish/functions/sxu.fish

echo 'function sxls --description "SX List - List files on server"
    ~/.dotnet/tools/sx sxls $argv
end' > ~/.config/fish/functions/sxls.fish
```

## 🗑️ Uninstall

```bash
# Remove tool
dotnet tool uninstall -g SX.Client

# Remove wrapper scripts
rm -f ~/.local/bin/sxd ~/.local/bin/sxu ~/.local/bin/sxls

# Remove completion
rm -rf ~/.sx/

# Remove shell config additions (manual cleanup)
```

## 📋 Requirements

- **.NET 9.0 or later**
- **SSH 2.0 or later** (for reverse port forwarding support)

## 🚧 Known Issues & Planned Features

### Known Issues
- **Upload disconnection detection**: Server detects client disconnections during upload with ~1 second delay

### Planned Features
- 🔄 **v1.0.3**: Multiple simultaneous client support with request queuing
- 🔄 **Future**: Resume interrupted transfers
- 🔄 **Future**: Directory synchronization

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable  
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with [.NET 9](https://dotnet.microsoft.com/)
- UI powered by [Spectre.Console](https://spectreconsole.net/)
- JSON handling via [Newtonsoft.Json](https://www.newtonsoft.com/json)
- Development assistance by [Claude](https://claude.ai) (Anthropic)

---

**Made with ❤️ for seamless SSH file transfers**

## Disclaimer

This software is provided "as is" for development and productivity purposes. While designed with security in mind through SSH tunnels, users are responsible for:

- Ensuring secure SSH configurations
- Validating file transfer permissions
- Implementing appropriate access controls
- Compliance with organizational security policies

Use in production environments is at your own discretion and risk.
