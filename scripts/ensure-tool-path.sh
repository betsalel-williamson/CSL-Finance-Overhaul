#!/usr/bin/env bash
# Ensure contributor tools are on PATH (dotnet) for hooks & scripts.
set -euo pipefail

if [[ -x "$HOME/.dotnet/dotnet" ]]; then
  export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
  case ":$PATH:" in
    *":$HOME/.dotnet:"*) ;;
    *) export PATH="$HOME/.dotnet:$PATH" ;;
  esac
fi
