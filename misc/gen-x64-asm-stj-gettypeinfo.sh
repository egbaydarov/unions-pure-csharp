#!/usr/bin/env bash
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/.." && pwd)"

project="$root/Unions.Pure.Csharp.Demo/Unions.Pure.Csharp.Demo.csproj"
out_dir="$here/out"
mkdir -p "$out_dir"

ts="$(date -u +%Y%m%dT%H%M%SZ)"
out_file="$out_dir/unions-pure-csharp-demo-x64-asm-stj-gettypeinfo-$ts.txt"

arch="$(uname -m)"
if [[ "$arch" != "x86_64" && "$arch" != "amd64" ]]; then
  echo "ERROR: This script is intended for x64 hosts. Detected uname -m: $arch" >&2
  exit 2
fi

echo "Building demo (Release)…" >&2
dotnet build "$project" -c Release -v minimal

echo "Running demo with System.Text.Json GetTypeInfo disasm…" >&2
echo "Output: $out_file" >&2

(
  export COMPlus_TieredCompilation=0
  export COMPlus_ReadyToRun=0

  # Disassemble STJ's hot metadata path, which is where resolver-vs-reflection diverges.
  export COMPlus_JitDisasm="System.Text.Json.JsonSerializer:GetTypeInfo*"
  export COMPlus_JitDisasmAssemblies="System.Text.Json"
  export COMPlus_JitDisasmDiffable=1
  export COMPlus_JitPrintInlinedMethods=1

  dotnet run --project "$project" -c Release --no-build
) >"$out_file" 2>&1

echo "Done." >&2
echo "$out_file"


