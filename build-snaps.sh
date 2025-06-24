#!/bin/bash
set -e

echo "🏗️  Building SX Snap Packages"
echo "============================="

# Check if snapcraft is available
if ! command -v snapcraft &> /dev/null; then
    echo "❌ snapcraft not found. Install with: sudo snap install snapcraft --classic"
    echo "ℹ️  Note: This script requires snapcraft (not available in WSL1)"
    exit 1
fi

# Build client snap
echo "📦 Building SX Client snap..."
cd snap-client
snapcraft --verbosity=verbose
cd ..

# Build server snap
echo "📦 Building SX Server snap..."
cd snap-server
snapcraft --verbosity=verbose
cd ..

echo "✅ Snap builds completed!"
echo ""
echo "📁 Generated snap packages:"
ls -la snap-client/*.snap snap-server/*.snap 2>/dev/null || echo "No snap files found"

echo ""
echo "🚀 To install locally:"
echo "  sudo snap install --dangerous --classic snap-client/sx-client_1.0.0_amd64.snap"
echo "  sudo snap install --dangerous --classic snap-server/sx-server_1.0.0_amd64.snap"