# Snap Package Setup Guide

This guide explains how to set up automated snap building and publishing for the SX project.

## Snap Store Registration

### 1. Register Snap Names

First, register the snap names in the Snap Store:

```bash
# Register client snap
snapcraft register sx-client

# Register server snap  
snapcraft register sx-server
```

### 2. Generate Store Credentials

Generate credentials for automated publishing:

```bash
# Export credentials (this will prompt for your Ubuntu One email and password)
snapcraft export-login credentials.txt
```

### 3. Add Credentials to GitHub

1. Go to your GitHub repository settings
2. Navigate to **Secrets and variables** → **Actions**
3. Click **New repository secret**
4. Name: `SNAPCRAFT_STORE_CREDENTIALS`
5. Value: Contents of `credentials.txt` file
6. Click **Add secret**

## GitHub Actions Workflow

The workflow in `.github/workflows/build-snaps.yml` will:

1. **On every push/PR**: Build snaps and upload as artifacts
2. **On tagged releases**: Build, publish to Snap Store, and attach to GitHub release

### Triggering a Release

To publish snaps to the Snap Store:

```bash
# Create and push a version tag
git tag v1.0.0
git push origin v1.0.0
```

This will:
- Build both client and server snaps
- Publish to Snap Store (stable channel)
- Create GitHub release with snap files attached

## Local Testing (requires snapcraft)

```bash
# Build both snaps locally
./build-snaps.sh

# Install locally for testing
sudo snap install --dangerous --classic snap-client/sx-client_1.0.0_amd64.snap
sudo snap install --dangerous --classic snap-server/sx-server_1.0.0_amd64.snap
```

## Snap Package Structure

The project uses separate directories for snap configurations:

```
SX/
├── snap-client/snapcraft.yaml    # Client snap configuration
├── snap-server/snapcraft.yaml    # Server snap configuration
└── .github/workflows/build-snaps.yml
```

### Client Package (sx-client)
- Location: `snap-client/snapcraft.yaml`
- Commands: `sxd`, `sxu`, `sxls`
- Confinement: classic (for SSH access)
- Dependencies: .NET 9.0 runtime

### Server Package (sx-server)  
- Location: `snap-server/snapcraft.yaml`
- Commands: `sx-server`
- Confinement: classic (for filesystem access)
- Dependencies: .NET 9.0 runtime

## Installation for Users

### Via Snap Store (after publishing)
```bash
# Install client on remote servers
sudo snap install sx-client

# Install server on local machine
sudo snap install sx-server
```

### Via GitHub Releases
```bash
# Download from releases page
wget https://github.com/Memphizzz/sx/releases/download/v1.0.0/sx-client_1.0.0_amd64.snap
sudo snap install --dangerous --classic sx-client_1.0.0_amd64.snap
```

## Updating Versions

1. Update version in both `snapcraft.yaml` files
2. Update version in `SX.Client.csproj` and `SX.Server.csproj`
3. Create new git tag matching the version
4. Push tag to trigger automated build and publish

## WSL1 Limitations

Since WSL1 doesn't support snapd:
- Use GitHub Actions for all snap building
- Local testing requires native Linux or WSL2
- The `build-snaps.sh` script will detect this limitation