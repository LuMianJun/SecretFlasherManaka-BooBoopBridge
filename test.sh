#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
configuration=Release
game_project=""
hardware_project=""
while (( $# )); do
  case "$1" in
    --configuration) configuration="${2:?Missing configuration}"; shift 2 ;;
    --game-project) game_project="${2:?Missing project path}"; shift 2 ;;
    --hardware-project) hardware_project="${2:?Missing project path}"; shift 2 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
args=()
[[ -z "$game_project" ]] || args+=("-p:GameSignalsProject=$game_project")
[[ -z "$hardware_project" ]] || args+=("-p:HardwareControlProject=$hardware_project")
dotnet run --project "$repo_root/tests/Bridge.MockTests/Bridge.MockTests.csproj" -c "$configuration" "${args[@]}"
