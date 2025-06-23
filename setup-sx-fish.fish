#!/usr/bin/env fish
# SX Setup Script for Fish Shell - Creates fish functions for SX commands

set -l RED '\033[0;31m'
set -l GREEN '\033[0;32m'
set -l YELLOW '\033[1;33m'
set -l BLUE '\033[0;34m'
set -l NC '\033[0m' # No Color

echo -e "$YELLOW"'SX Setup - Creating Fish Functions'"$NC"
echo -e "$YELLOW"'================================='"$NC"
echo ""

# Check if sx tool is installed
if not test -e ~/.dotnet/tools/sx
    echo -e "$RED"'❌ SX tool not found. Please install it first:'"$NC"
    echo '   dotnet tool install -g SX.Client'
    echo ""
    echo -e "$BLUE"'Or install from local build:'"$NC"
    echo '   dotnet tool install --global --add-source ./packages SX.Client'
    exit 1
end

echo -e "$GREEN"'✅ SX tool found'"$NC"
echo ""

# Create fish functions directory
set -l FISH_FUNCTIONS_DIR "$HOME/.config/fish/functions"
mkdir -p "$FISH_FUNCTIONS_DIR"

echo -e "$BLUE"'Creating fish functions in '"$FISH_FUNCTIONS_DIR"'...'"$NC"

# Create sxd function
echo 'function sxd --description "SX Download - Get file from server"
    ~/.dotnet/tools/sx sxd $argv
end' > "$FISH_FUNCTIONS_DIR/sxd.fish"

# Create sxu function
echo 'function sxu --description "SX Upload - Send file to server"
    ~/.dotnet/tools/sx sxu $argv
end' > "$FISH_FUNCTIONS_DIR/sxu.fish"

# Create sxls function
echo 'function sxls --description "SX List - List files on server"
    ~/.dotnet/tools/sx sxls $argv
end' > "$FISH_FUNCTIONS_DIR/sxls.fish"

echo -e "$GREEN"'✅ Fish functions created:'"$NC"
echo "   $FISH_FUNCTIONS_DIR/sxd.fish"
echo "   $FISH_FUNCTIONS_DIR/sxu.fish"
echo "   $FISH_FUNCTIONS_DIR/sxls.fish"
echo ""

# Test the functions
echo -e "$BLUE"'Testing installation...'"$NC"
if fish -c 'sxd --help' >/dev/null 2>&1
    echo -e "$GREEN"'✅ SX functions working'"$NC"
else
    echo -e "$RED"'❌ SX function test failed'"$NC"
    exit 1
end

echo ""
echo -e "$GREEN"'🎉 Fish setup completed successfully!'"$NC"
echo ""
echo -e "$YELLOW"'Next steps:'"$NC"
echo '1. Functions are ready to use!'
echo '2. Start SX server locally: sx-server --dir ~/Downloads'
echo '3. Reconnect this SSH session with tunnel: ssh -R 53690:localhost:53690 user@server'
echo '4. Use commands: sxd, sxu, sxls'
echo ""
echo -e "$BLUE"'Example usage:'"$NC"
echo '   sxls                    # List files'
echo '   sxd largefile.zip       # Download file'
echo '   sxu myfile.txt          # Upload file'
echo ""
echo -e "$BLUE"'Fish-specific features:'"$NC"
echo '   - Tab completion for function names'
echo '   - Built-in help: help sxd, help sxu, help sxls'
echo '   - Functions available in all new fish sessions'