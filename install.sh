#!/bin/bash

# LfsMinio Auto Installer for Unix-like systems (Linux, macOS)
# This script downloads the latest release and installs it to ~/.lfs-mirror

set -e

INSTALL_DIR="$HOME/.lfs-mirror"

echo "LfsMinio Auto Installer"
echo "======================"

# Create install directory
if [ ! -d "$INSTALL_DIR" ]; then
    echo "Creating install directory: $INSTALL_DIR"
    mkdir -p "$INSTALL_DIR"
fi

# Get latest release info
echo "Fetching latest release information..."
RELEASE_INFO=$(curl -s "https://api.github.com/repos/li-zhixin/LfsMinio/releases/latest")

if [ $? -ne 0 ]; then
    echo "Error: Failed to fetch release information"
    exit 1
fi

VERSION=$(echo "$RELEASE_INFO" | grep '"tag_name":' | sed -E 's/.*"tag_name": *"([^"]+)".*/\1/')
echo "Latest version: $VERSION"

# Determine OS and architecture
OS=""
ARCH=""

case "$(uname -s)" in
    Linux*)     OS="linux";;
    Darwin*)    OS="osx";;
    CYGWIN*)    OS="win";;
    MINGW*)     OS="win";;
    *)          echo "Unsupported OS: $(uname -s)"; exit 1;;
esac

case "$(uname -m)" in
    x86_64|amd64)   ARCH="x64";;
    i386|i686)      ARCH="x86";;
    aarch64|arm64)  ARCH="arm64";;
    armv7l)         ARCH="arm";;
    *)              echo "Unsupported architecture: $(uname -m)"; exit 1;;
esac

echo "Detected platform: $OS-$ARCH"

# Find the appropriate asset
ASSET_NAME="LfsMinio-$OS-$ARCH"
DOWNLOAD_URL=$(echo "$RELEASE_INFO" | grep "browser_download_url" | grep "$ASSET_NAME" | head -n 1 | cut -d '"' -f 4)

if [ -z "$DOWNLOAD_URL" ]; then
    echo "Error: No suitable release found for $OS-$ARCH"
    exit 1
fi

FILENAME=$(basename "$DOWNLOAD_URL")
TEMP_FILE="/tmp/$FILENAME"

echo "Downloading $FILENAME..."
curl -L -o "$TEMP_FILE" "$DOWNLOAD_URL"

if [ $? -ne 0 ]; then
    echo "Error: Failed to download release"
    exit 1
fi

# Extract archive
echo "Extracting to $INSTALL_DIR..."
case "$FILENAME" in
    *.tar.gz)
        tar -xzf "$TEMP_FILE" -C "$INSTALL_DIR"
        ;;
    *.zip)
        if command -v unzip >/dev/null 2>&1; then
            unzip -o "$TEMP_FILE" -d "$INSTALL_DIR"
        else
            echo "Error: unzip command not found"
            exit 1
        fi
        ;;
    *)
        # Assume it's a single binary
        cp "$TEMP_FILE" "$INSTALL_DIR/"
        chmod +x "$INSTALL_DIR/$(basename "$TEMP_FILE")"
        ;;
esac

# Clean up
rm -f "$TEMP_FILE"

# Make binary executable
find "$INSTALL_DIR" -name "LfsMinio" -type f -exec chmod +x {} \;

# Add to PATH
SHELL_RC=""
case "$SHELL" in
    */bash)
        SHELL_RC="$HOME/.bashrc"
        ;;
    */zsh)
        SHELL_RC="$HOME/.zshrc"
        ;;
    */fish)
        SHELL_RC="$HOME/.config/fish/config.fish"
        ;;
    *)
        SHELL_RC="$HOME/.profile"
        ;;
esac

# Check if already in PATH
if ! echo "$PATH" | grep -q "$INSTALL_DIR"; then
    echo "Adding $INSTALL_DIR to PATH..."
    
    if [ -f "$SHELL_RC" ]; then
        if ! grep -q "$INSTALL_DIR" "$SHELL_RC"; then
            echo "" >> "$SHELL_RC"
            echo "# LfsMinio" >> "$SHELL_RC"
            echo "export PATH=\"\$PATH:$INSTALL_DIR\"" >> "$SHELL_RC"
        fi
    else
        echo "export PATH=\"\$PATH:$INSTALL_DIR\"" > "$SHELL_RC"
    fi
    
    # Update current session PATH
    export PATH="$PATH:$INSTALL_DIR"
    
    echo "Added to PATH in $SHELL_RC"
    echo "Please restart your terminal or run: source $SHELL_RC"
else
    echo "Directory already in PATH."
fi

echo ""
echo "Installation completed successfully!"
echo "LfsMinio has been installed to: $INSTALL_DIR"
echo ""
echo "You can now run 'lfs-minio' from anywhere in your terminal."
echo "If the command is not found, please restart your terminal or reload your shell configuration."