#!/bin/bash
# SX Setup Script - Creates wrapper scripts for SX commands

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${YELLOW}SX Setup - Creating Command Wrappers${NC}"
echo -e "${YELLOW}=====================================${NC}"
echo ""

# Check if sx tool is installed
if ! test -e ~/.dotnet/tools/sx; then
    echo -e "${RED}❌ SX tool not found. Please install it first:${NC}"
    echo "   dotnet tool install -g SX.Client"
    echo ""
    echo -e "${BLUE}Or install from local build:${NC}"
    echo "   dotnet tool install --global --add-source ./packages SX.Client"
    exit 1
fi

echo -e "${GREEN}✅ SX tool found${NC}"
echo ""

# Get user's local bin directory
LOCAL_BIN="$HOME/.local/bin"
mkdir -p "$LOCAL_BIN"

echo -e "${BLUE}Creating wrapper scripts in $LOCAL_BIN...${NC}"

# Create sxd wrapper
cat > "$LOCAL_BIN/sxd" << 'EOF'
#!/bin/bash
exec ~/.dotnet/tools/sx sxd "$@"
EOF

# Create sxu wrapper  
cat > "$LOCAL_BIN/sxu" << 'EOF'
#!/bin/bash
exec ~/.dotnet/tools/sx sxu "$@"
EOF

# Create sxls wrapper
cat > "$LOCAL_BIN/sxls" << 'EOF'
#!/bin/bash
exec ~/.dotnet/tools/sx sxls "$@"
EOF

# Make scripts executable
chmod +x "$LOCAL_BIN/sxd" "$LOCAL_BIN/sxu" "$LOCAL_BIN/sxls"

echo -e "${GREEN}✅ Wrapper scripts created:${NC}"
echo "   $LOCAL_BIN/sxd"
echo "   $LOCAL_BIN/sxu"
echo "   $LOCAL_BIN/sxls"
echo ""

# Check if ~/.local/bin is in PATH
if [[ ":$PATH:" != *":$LOCAL_BIN:"* ]]; then
    echo -e "${YELLOW}⚠️  $LOCAL_BIN is not in your PATH${NC}"
    echo -e "${BLUE}Adding to PATH in shell configuration...${NC}"
    
    # Detect shell and add to appropriate config
    SHELL_NAME=$(basename "$SHELL")
    case "$SHELL_NAME" in
        "bash")
            SHELL_RC="$HOME/.bashrc"
            ;;
        "zsh") 
            SHELL_RC="$HOME/.zshrc"
            ;;
        "fish")
            echo -e "${BLUE}Fish shell detected!${NC}"
            echo -e "${YELLOW}For fish shell, use the dedicated setup script instead:${NC}"
            echo "   ./setup-sx-fish.fish"
            echo ""
            echo -e "${BLUE}This provides native fish functions with better integration.${NC}"
            exit 0
            ;;
        *)
            echo -e "${YELLOW}Unknown shell: $SHELL_NAME. Defaulting to ~/.bashrc${NC}"
            SHELL_RC="$HOME/.bashrc"
            ;;
    esac
    
    if [[ "$SHELL_NAME" != "fish" ]]; then
        # Check if PATH export already exists
        if ! grep -q "export PATH.*$LOCAL_BIN" "$SHELL_RC" 2>/dev/null; then
            echo "" >> "$SHELL_RC"
            echo "# Add local bin to PATH for SX commands" >> "$SHELL_RC"
            echo "export PATH=\"$LOCAL_BIN:\$PATH\"" >> "$SHELL_RC"
            echo -e "${GREEN}✅ Added $LOCAL_BIN to PATH in $SHELL_RC${NC}"
        else
            echo -e "${GREEN}✅ PATH already configured in $SHELL_RC${NC}"
        fi
    fi
else
    echo -e "${GREEN}✅ $LOCAL_BIN already in PATH${NC}"
fi

echo ""
echo -e "${BLUE}Testing installation...${NC}"
if "$LOCAL_BIN/sxd" --help >/dev/null 2>&1; then
    echo -e "${GREEN}✅ SX commands working${NC}"
else
    echo -e "${RED}❌ SX command test failed${NC}"
    exit 1
fi

echo ""
echo -e "${GREEN}🎉 Setup completed successfully!${NC}"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
if [[ ":$PATH:" != *":$LOCAL_BIN:"* ]]; then
    echo "1. Restart your shell or run: source $SHELL_RC"
else
    echo "1. Commands are ready to use!"
fi
echo "2. Start SX server locally: sx-server --dir ~/Downloads"
echo "3. Reconnect this SSH session with tunnel: ssh -R 53690:localhost:53690 user@server"
echo "4. Use commands: sxd, sxu, sxls"
echo ""
echo -e "${BLUE}Example usage:${NC}"
echo "   sxls                    # List files"
echo "   sxd largefile.zip       # Download file"  
echo "   sxu myfile.txt          # Upload file"