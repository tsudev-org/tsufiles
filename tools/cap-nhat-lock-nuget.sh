#!/usr/bin/env bash
# TsuFiles: sinh lại mọi lock file NuGet (packages.lock.json và packages.lock.no-wayland.json) sau khi đổi
# Directory.Packages.props hoặc PackageReference - kể cả trên nhánh PR Dependabot NuGet (Dependabot chạy trên Linux
# nên không cập nhật packages.lock.no-wayland.json; CI Windows chạy --locked-mode sẽ đỏ tới khi chạy script này).
#
# Cách dùng (Linux/WSL, .NET SDK theo global.json):  tools/cap-nhat-lock-nuget.sh
# Kiểm không đổi gì (như CI):                         tools/cap-nhat-lock-nuget.sh --kiem
set -euo pipefail
cd "$(dirname "$0")/.."

if [[ "$(uname -s)" != Linux ]]; then
  echo "LỖI: chạy trên Linux/WSL - packages.lock.json là đồ thị gói của bản Linux (có Avalonia.Wayland)." >&2
  exit 1
fi

if [[ "${1:-}" == --kiem ]]; then
  # Bản không-Wayland trước để obj/ cuối cùng mang đồ thị Linux của máy này.
  dotnet restore TsuFiles.slnx --locked-mode -p:TsuFilesNativeWayland=false
  dotnet restore TsuFiles.slnx --locked-mode
  echo "Lock file khớp."
  exit 0
fi

# Bản Linux (có Wayland) -> packages.lock.json; bản Windows/macOS -> packages.lock.no-wayland.json ở các dự án đặt
# TsuFilesLockPerWayland=true (Directory.Build.targets). --force-evaluate: bỏ qua lock file cũ, giải lại đồ thị.
dotnet restore TsuFiles.slnx --force-evaluate
dotnet restore TsuFiles.slnx --force-evaluate -p:TsuFilesNativeWayland=false
# Trả obj/ về đồ thị của máy này để build tiếp không lẫn bản không-Wayland.
dotnet restore TsuFiles.slnx --locked-mode
git status --short -- '*packages.lock*.json'
