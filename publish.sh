#!/bin/bash
set -e
cd "$(dirname "$0")"

echo "=== 发布 MirrorVault ==="
echo ""

# Clean
rm -rf publish/linux-x64 publish/win-x64
mkdir -p publish/linux-x64 publish/win-x64

# Linux
echo ">>> 发布 Linux x64..."
dotnet publish BackupApp.UI/BackupApp.UI.csproj \
    -c Release -r linux-x64 --self-contained \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o publish/linux-x64
echo "    Linux: $(ls -lh publish/linux-x64/MirrorVault | awk '{print $5}')"

# Windows
echo ">>> 发布 Windows x64..."
dotnet publish BackupApp.UI/BackupApp.UI.csproj \
    -c Release -r win-x64 --self-contained \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o publish/win-x64
echo "    Windows: $(ls -lh publish/win-x64/MirrorVault.exe | awk '{print $5}')"

echo ""
echo "=== 发布完成 ==="
echo "Linux:   publish/linux-x64/MirrorVault"
echo "Windows: publish/win-x64/MirrorVault.exe"
