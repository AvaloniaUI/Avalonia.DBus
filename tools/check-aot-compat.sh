#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

PROJECT="src/Avalonia.DBus/Avalonia.DBus.csproj"
SOURCE_DIR="src/Avalonia.DBus"
FORBIDDEN_PATTERN='Activator\.CreateInstance|MakeGenericType|BindingFlags|GetMethod\(|GetProperty\(|GetField\(|MethodInfo|System\.Reflection\.Emit|\bdynamic\b'

echo "==> AOT analyzer gate ($PROJECT)"
dotnet build "$PROJECT" -c Release --no-restore \
  -p:IsAotCompatible=true \
  -p:EnableTrimAnalyzer=true \
  -p:EnableSingleFileAnalyzer=true \
  -p:EnableAotAnalyzer=true \
  -warnaserror

echo "==> Static non-AOT API guard ($SOURCE_DIR)"
if command -v rg >/dev/null 2>&1; then
  FIND_CMD=(rg -n --glob '*.cs' -e "$FORBIDDEN_PATTERN" "$SOURCE_DIR")
else
  FIND_CMD=(grep -RInE --include='*.cs' "$FORBIDDEN_PATTERN" "$SOURCE_DIR")
fi

if "${FIND_CMD[@]}"; then
  echo
  echo "AOT guard failed: forbidden reflection/dynamic patterns were found in $SOURCE_DIR."
  exit 1
fi

echo "AOT compatibility checks passed."
