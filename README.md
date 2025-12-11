# M3U List Editor

A fast, cross-platform CLI tool to clean M3U/M3U8 playlists by removing dead links and duplicates.

[![Build and Release](https://github.com/andiq123/M3uListEditor/actions/workflows/release.yml/badge.svg)](https://github.com/andiq123/M3uListEditor/actions/workflows/release.yml)

## Features

- Validates stream links by actually testing connectivity
- Removes duplicate channels (by URL and name)
- Supports local files and remote URLs
- Cross-platform (Windows, macOS, Linux)
- Parallel processing for fast scanning
- Self-contained single executable (no .NET runtime required)

## Download

Download the latest release for your platform from [Releases](https://github.com/andiq123/M3uListEditor/releases):

| Platform | File |
|----------|------|
| Windows x64 | `m3u-editor-win-x64.exe` |
| macOS Intel | `m3u-editor-osx-x64` |
| macOS Apple Silicon | `m3u-editor-osx-arm64` |
| Linux x64 | `m3u-editor-linux-x64` |
| Linux ARM64 | `m3u-editor-linux-arm64` |

## Quick Start

### Interactive Mode

Simply run the executable and follow the prompts:

```bash
# macOS/Linux - make executable first
chmod +x m3u-editor-*

# Run
./m3u-editor-osx-arm64
```

```
╔════════════════════════════════════════╗
║        M3U LIST EDITOR v2.0            ║
╚════════════════════════════════════════╝

  Drag and drop an M3U file here, or paste a URL:
  >
```

### Command Line Mode

```bash
# Clean a local file
./m3u-editor -src playlist.m3u

# Clean from URL
./m3u-editor -src https://example.com/playlist.m3u

# Specify output path
./m3u-editor -src playlist.m3u -dest cleaned.m3u

# Custom timeout and concurrency
./m3u-editor -src playlist.m3u -timeout 15 -c 20
```

## Options

| Option | Description | Default |
|--------|-------------|---------|
| `-src, --source <path>` | Source M3U file path or URL | (interactive) |
| `-dest, --destination <path>` | Output file path | `{temp}/M3uListEditor/{name}-Cleaned.m3u` |
| `-timeout, --to <seconds>` | Connection timeout per channel | 10 |
| `-c, --concurrency <n>` | Parallel connections (1-50) | 10 |
| `-rd, --removedoubles <bool>` | Remove duplicate channels | true |
| `-v, --verbose` | Show detailed error info | false |
| `-h, --help` | Show help | |

## Examples

```bash
# Basic usage - clean a local file
./m3u-editor -src ~/Downloads/iptv.m3u

# Download and clean from URL, save to specific location
./m3u-editor -src https://example.com/list.m3u -dest ~/Desktop/clean.m3u

# Faster scanning with higher concurrency and shorter timeout
./m3u-editor -src playlist.m3u -c 30 -timeout 5

# Keep duplicates
./m3u-editor -src playlist.m3u -rd false
```

## Output Example

```
╔════════════════════════════════════════╗
║        M3U LIST EDITOR v2.0            ║
╚════════════════════════════════════════╝

  CONFIGURATION
  ─────────────────────────────────────
  Source:      playlist.m3u
  Destination: /tmp/M3uListEditor/playlist-Cleaned.m3u
  ─────────────────────────────────────
  Timeout: 10s  │  Concurrency: 10  │  Remove duplicates: Yes
  ─────────────────────────────────────
  Tip: Use -dest <path> to change output, -h for all options

  [██████████████████████████████] 100%
  ✓ 142    ✗ 58     Total: 200/200

╔════════════════════════════════════════╗
║            ✓ COMPLETE!                 ║
╚════════════════════════════════════════╝

  RESULTS
  ─────────────────────────────────────
  Working:    142 / 200  (71.0%)
  Failed:     58
  Duplicates: 12 removed
  ─────────────────────────────────────
  OUTPUT FILE
  ─────────────────────────────────────
  Path: /tmp/M3uListEditor/playlist-Cleaned.m3u
  Size: 45.2 KB
  ─────────────────────────────────────
```

## Building from Source

Requirements: [.NET 9 SDK](https://dotnet.microsoft.com/download)

```bash
# Clone
git clone https://github.com/andiq123/M3uListEditor.git
cd M3uListEditor

# Run directly
dotnet run --project UI -- -h

# Build release
dotnet publish UI/UI.csproj -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o ./publish
```

## How It Works

1. **Parse** - Reads the M3U file and extracts channel metadata (name, group, URL)
2. **Deduplicate** - Removes duplicate channels by normalized URL and name
3. **Validate** - Tests each stream URL with retry logic:
   - Sends HTTP request with streaming headers
   - Validates content-type is a media stream
   - Reads initial bytes to verify actual stream data
   - Detects HTML error pages masquerading as streams
4. **Export** - Writes only working channels to the output file

## License

MIT
