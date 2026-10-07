#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
game_dir="${BOOBOOP_GAME_DIR:-}"
configuration=Release
game_project=""
hardware_project=""
mock_only=false
while (( $# )); do
  case "$1" in
    --game-dir) game_dir="${2:?Missing game directory}"; shift 2 ;;
    --configuration) configuration="${2:?Missing configuration}"; shift 2 ;;
    --game-project) game_project="${2:?Missing project path}"; shift 2 ;;
    --hardware-project) hardware_project="${2:?Missing project path}"; shift 2 ;;
    --mock-only) mock_only=true; shift ;;
    -h|--help) echo 'Usage: bash build.sh --game-dir PATH [--configuration Release] [--game-project PATH] [--hardware-project PATH] [--mock-only]'; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
if ! "$mock_only" && [[ -z "$game_dir" ]]; then echo 'Set --game-dir or BOOBOOP_GAME_DIR.' >&2; exit 2; fi
test_args=(--configuration "$configuration")
build_args=()
[[ -z "$game_project" ]] || { test_args+=(--game-project "$game_project"); build_args+=("-p:GameSignalsProject=$game_project"); }
[[ -z "$hardware_project" ]] || { test_args+=(--hardware-project "$hardware_project"); build_args+=("-p:HardwareControlProject=$hardware_project"); }
bash "$repo_root/test.sh" "${test_args[@]}"
if "$mock_only"; then exit 0; fi
dotnet build "$repo_root/SecretFlasherManaka.BooBoopBridge.csproj" -c "$configuration" "-p:GameDir=$game_dir" "${build_args[@]}"
stage="$repo_root/artifacts/plugins/SecretFlasherManakaBooBoop"
mkdir -p "$stage"
for name in SecretFlasherManaka.ForEveryThing BooBoopControl SecretFlasherManaka.BooBoopBridge; do
  cp -- "$repo_root/bin/$configuration/net6.0/$name.dll" "$stage/$name.dll"
done
printf 'Tests and build passed. Three plugin DLLs staged at: %s\n' "$stage"
