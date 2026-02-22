# Building Vido

Instructions for building Vido from source, creating portable distributions, and producing MSI installers.

## Prerequisites

| Requirement | Version | Notes |
|---|---|---|
| .NET SDK | 8.0+ | [Download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| WiX Toolset CLI | 5.0.2 | Required only for MSI installer builds |
| Windows | 10+ (x64) | WPF application — Windows only |

### Installing WiX Toolset (optional, for MSI builds)

```powershell
dotnet tool install --global wix --version 5.0.2
wix extension add WixToolset.UI.wixext/5.0.2 -g
wix extension add WixToolset.Util.wixext/5.0.2 -g
```

## Quick Start

```powershell
# Clone and build
git clone https://github.com/your-org/vido.git
cd vido
dotnet build Vido.sln
```

## Development Build

```powershell
# Build all projects in Debug configuration
dotnet build Vido.sln

# Run the application
dotnet run --project src/Vido.App

# Run tests
dotnet test tests/Vido.Tests
```

## Release Build

The `build-release.ps1` script automates the entire release process. It produces:

- **Portable zip** — A self-contained archive that runs without installation
- **MSI installer** — A Windows Installer package with shortcuts and file associations

### Full Release (Portable + MSI)

```powershell
.\build-release.ps1
```

### Portable Only (skip MSI)

```powershell
.\build-release.ps1 -SkipInstaller
```

### Custom Output Directory

```powershell
.\build-release.ps1 -OutputDir "dist"
```

### Script Parameters

| Parameter | Default | Description |
|---|---|---|
| `-Configuration` | `Release` | Build configuration (`Release` or `Debug`) |
| `-SkipInstaller` | `$false` | Skip MSI installer creation |
| `-OutputDir` | `publish` | Directory for final artifacts |

### Build Output

After a successful build, the `publish/` directory contains:

```
publish/
  portable/                              # Unpacked self-contained application
  Vido-0.1.0-win-x64-portable.zip       # ~142 MB — portable distribution
  Vido-0.1.0-win-x64.msi                # ~109 MB — MSI installer
```

## Distribution Details

### Portable Distribution

The portable zip is a fully self-contained .NET 8 application:

- **No prerequisites** — includes the .NET runtime
- **ReadyToRun** — pre-compiled for faster cold startup
- **Extract and run** — unzip anywhere, launch `Vido.exe`
- **Size** — ~142 MB compressed, ~355 MB uncompressed, ~513 files

### MSI Installer

The MSI installer provides a traditional Windows installation experience:

- **Per-user install** — installs to `%LocalAppData%\Vido` (no admin required)
- **Start Menu shortcut** — always created
- **Desktop shortcut** — optional feature (enabled by default)
- **File associations** — optional feature for `.mp4`, `.avi`, `.mkv`, `.mov`, `.wmv`, `.flv`, `.webm`
- **Upgrade support** — newer versions automatically replace older installs
- **Clean uninstall** — removes all files, shortcuts, and registry entries

### Installer Features

During installation, users can choose which optional features to include:

| Feature | Default | Description |
|---|---|---|
| Vido Video Player | Always | Core application (required) |
| Desktop Shortcut | On | Shortcut on the Windows Desktop |
| File Associations | On | Associate video file types with Vido |

## Project Structure

```
Vido.sln                    # Solution file
build-release.ps1           # Release build script
installer/
  Vido.Installer.wixproj    # WiX 5 SDK project
  Package.wxs               # Installer definition (features, shortcuts, associations)
src/
  Vido.App/                 # WPF application entry point
  Vido.Core/                # Core interfaces and models
  Vido.Services/            # Service implementations (FFmpeg, settings, etc.)
  Vido.ViewModels/          # MVVM view models
  Vido.Views/               # WPF views and controls
  Vido.PluginHost/          # Plugin loading and management
tests/
  Vido.Tests/               # xUnit test project
```

## Versioning

The application version is defined in `src/Vido.App/Vido.App.csproj`:

```xml
<Version>0.1.0</Version>
```

The build script reads this version automatically and uses it for:

- Portable zip filename (`Vido-{version}-win-x64-portable.zip`)
- MSI filename (`Vido-{version}-win-x64.msi`)
- MSI product version

## Troubleshooting

### WiX build fails with "wix not found"

Install the WiX CLI as a .NET global tool:

```powershell
dotnet tool install --global wix --version 5.0.2
```

### WiX build fails with missing extensions

Install the required WiX extensions globally:

```powershell
wix extension add WixToolset.UI.wixext/5.0.2 -g
wix extension add WixToolset.Util.wixext/5.0.2 -g
```

### Tests fail with FFmpeg errors

FFmpeg native binaries are included via the `FFmpeg.LGPL` NuGet package. Ensure NuGet restore completes successfully:

```powershell
dotnet restore Vido.sln
```

### Build produces large output

The self-contained publish includes the .NET runtime and FFmpeg native libraries. This is expected — the portable zip compresses to ~142 MB with lossless compression.
