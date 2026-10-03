#!/usr/bin/env bash
# Local build for the Antra Lidarr plugin (mirrors the CI steps in
# .github/workflows/build.yml closely enough for daily dev).
#
# The critical step is the version patch: the vendored Lidarr source carries a
# wildcard AssemblyVersion (10.0.0.*), but the released builds use
# 3.0.0.4855. A plugin built against the wildcard version fails to load in a
# real Lidarr ("Could not load file or assembly 'Lidarr.Core, Version=10.0.0...'"),
# so pin it exactly like CI does before building.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MINIMUM_LIDARR_VERSION="3.0.0.4855"

cd "$REPO_ROOT"

sed -i'' -e "s/<AssemblyVersion>[0-9.*]\+<\/AssemblyVersion>/<AssemblyVersion>${MINIMUM_LIDARR_VERSION}<\/AssemblyVersion>/g" \
    ext/Lidarr/src/Directory.Build.props

echo "Patched ext/Lidarr AssemblyVersion -> ${MINIMUM_LIDARR_VERSION}:"
grep -n "<AssemblyVersion>" ext/Lidarr/src/Directory.Build.props | head -3

cd src
dotnet build Lidarr.Plugin.Antra.sln -c Release -f net8.0 -p:TreatWarningsAsErrors=false

echo
echo "Output: $REPO_ROOT/_plugins/net8.0/Lidarr.Plugin.Antra/"
ls -la "$REPO_ROOT/_plugins/net8.0/Lidarr.Plugin.Antra/Lidarr.Plugin.Antra.dll" 2>/dev/null || true
