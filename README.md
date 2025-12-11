# StreamForge

A fast, cross-platform CLI tool to clean and enhance M3U/M3U8 playlists.

[![Build and Release](https://github.com/andiq123/M3uListEditor/actions/workflows/release.yml/badge.svg)](https://github.com/andiq123/M3uListEditor/actions/workflows/release.yml)

## Features

- **Stream Validation** - Tests connectivity to filter dead links
- **Duplicate Removal** - Deduplicates by URL and name
- **Smart Categorization** - Auto-detects group (News, Sports, etc.) from name
- **Language Detection** - Identifies language from name/metadata (e.g. US, FR)
- **Channel Renamer** - Bulk rename channels with pattern matching
- **Cross-platform** - Works on Windows, macOS, and Linux
- **Parallel Processing** - Fast scanning with configurable concurrency
- **Self-contained** - Single executable, no runtime required

## Download

Download the latest release for your platform from [Releases](https://github.com/andiq123/M3uListEditor/releases):

| Platform | File |
|----------|------|
| Windows x64 | `streamforge-win-x64.exe` |
| macOS Intel | `streamforge-osx-x64` |
| macOS Apple Silicon | `streamforge-osx-arm64` |
| Linux x64 | `streamforge-linux-x64` |
| Linux ARM64 | `streamforge-linux-arm64` |

## Quick Start

### Interactive Mode

Simply run the executable and follow the prompts:

```bash
# macOS/Linux - make executable first
chmod +x streamforge-*

# Run
./streamforge-osx-arm64
```

```
   ███████╗████████╗██████╗ ███████╗ █████╗ ███╗   ███╗
   ██╔════╝╚══██╔══╝██╔══██╗██╔════╝██╔══██╗████╗ ████║
   ███████╗   ██║   ██████╔╝█████╗  ███████║██╔████╔██║
   ╚════██║   ██║   ██╔══██╗██╔══╝  ██╔══██╝██║╚██╔╝██║
   ███████║   ██║   ██║  ██║███████╗██║  ██║██║ ╚═╝ ██║
   ╚══════╝   ╚═╝   ╚═╝  ╚═╝╚══════╝╚═╝  ╚═╝╚═╝     ╚═╝
                          FORGE v2.0

  📁 Drop an M3U file here, or paste a URL:
  ❯
```

### Command Line Mode

```bash
# Clean a local file
streamforge -src playlist.m3u

# Clean from URL
streamforge -src https://example.com/playlist.m3u

# Specify output path
streamforge -src playlist.m3u -dest cleaned.m3u

# Custom timeout and concurrency
streamforge -src playlist.m3u -timeout 15 -c 20
```

## Options

| Option | Description | Default |
|--------|-------------|---------|
| `-src, --source <path>` | Source M3U file path or URL | (interactive) |
| `-dest, --destination <path>` | Output file path | `{temp}/StreamForge/{name}-Cleaned.m3u` |
| `-timeout, --to <seconds>` | Connection timeout per channel | 10 |
| `-c, --concurrency <n>` | Parallel connections (1-50) | 10 |
| `-rd, --removedoubles <bool>` | Remove duplicate channels | true |
| `-smart-cat` / `-no-smart-cat` | Auto-detect categories | true |
| `-lang` / `-no-lang` | Detect channel languages | true |
| `-deep-scan` | Detect duplicates by content hash | false |
| `-rename <pat> <repl>` | Rename channels (search/replace) | |
| `-v, --verbose` | Show detailed error info | false |
| `-h, --help` | Show help | |

## Examples

```bash
# Basic usage - clean a local file
streamforge -src ~/Downloads/iptv.m3u

# Download and clean from URL, save to specific location
streamforge -src https://example.com/list.m3u -dest ~/Desktop/clean.m3u

# Faster scanning with higher concurrency and shorter timeout
streamforge -src playlist.m3u -c 30 -timeout 5

# Keep duplicates
streamforge -src playlist.m3u -rd false
```

## Output Example

```
   ███████╗████████╗██████╗ ███████╗ █████╗ ███╗   ███╗
   ██╔════╝╚══██╔══╝██╔══██╗██╔════╝██╔══██╗████╗ ████║
   ███████╗   ██║   ██████╔╝█████╗  ███████║██╔████╔██║
   ╚════██║   ██║   ██╔══██╗██╔══╝  ██╔══██╝██║╚██╔╝██║
   ███████║   ██║   ██║  ██║███████╗██║  ██║██║ ╚═╝ ██║
   ╚══════╝   ╚═╝   ╚═╝  ╚═╝╚══════╝╚═╝  ╚═╝╚═╝     ╚═╝
                          FORGE v2.0

  ╭───────────────────────────────────────────────────────╮
  │  📁 Source      playlist.m3u                          │
  │  📤 Output      playlist-Cleaned.m3u                  │
  ├───────────────────────────────────────────────────────┤
  │  ⏱️  Timeout 10s    ⚡ Workers 10    🧹 Dedupe On      │
  ╰───────────────────────────────────────────────────────╯

  ⠹ Validating streams...

  ████████████████████████████████████████ 100%

  ┌─────────────────┬────────────┐
  │ ✓ Working       │ 142        │
  │ ✗ Failed        │ 47         │
  │ ⏳ Remaining    │ 0          │
  └─────────────────┴────────────┘

  ╭─────────────────── ✓ COMPLETE ───────────────────╮
  │                                                   │
  │  📊 Results                                       │
  │     ✓ 142 working   (75%)  ███████████████░░░░░   │
  │     ✗ 47 failed                                   │
  │     🗑️  12 duplicates removed                      │
  │                                                   │
  │  🏷️  Enhancements                                  │
  │     📦 8 groups   🎯 45 categorized   🌍 82 langs  │
  │                                                   │
  │  📁 Output                                        │
  │     playlist-Cleaned.m3u                          │
  │     Size: 45.2 KB                                 │
  │                                                   │
  ╰───────────────────────────────────────────────────╯
```

## Building from Source

Requirements: [.NET 9 SDK](https://dotnet.microsoft.com/download)

```bash
# Clone
git clone https://github.com/andiq123/M3uListEditor.git
cd M3uListEditor

# Run directly
dotnet run --project StreamForge -- -h

# Build release
dotnet publish StreamForge -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o ./publish
```

## How It Works

1. **Parse** - Reads the M3U file and extracts channel metadata (name, group, URL)
2. **Deduplicate** - Removes duplicate channels by normalized URL and name
3. **Enhance** - Applies smart features:
   - **Rename**: Applies string/regex replacement on names
   - **Categorize**: Detects group from channel name
   - **Language**: Detects language from country codes/keywords
4. **Validate** - Tests each stream URL with retry logic:
   - Sends HTTP request with streaming headers
   - Validates content-type is a media stream
   - Reads initial bytes to verify actual stream data
   - Detects HTML error pages masquerading as streams
5. **Export** - Writes only working channels to the output file

## License

MIT
