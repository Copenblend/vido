<#
.SYNOPSIS
    Builds Vido portable and installer distributions.

.DESCRIPTION
    Publishes Vido as a self-contained win-x64 application and creates:
      - A portable zip archive
      - A custom setup EXE installer (Vido.Setup)

    If a code signing certificate is provided, the EXE and setup are signed.
    Signing eliminates the Windows SmartScreen "Unknown Publisher" warning.

.PARAMETER Configuration
    Build configuration. Default: Release.

.PARAMETER SkipInstaller
    Skip setup EXE installer creation.

.PARAMETER OutputDir
    Directory for final artifacts. Default: ./publish.

.PARAMETER CertificatePath
    Path to a .pfx code signing certificate file. If provided, artifacts are signed.

.PARAMETER CertificatePassword
    Password for the .pfx certificate.

.PARAMETER SignTool
    Path to signtool.exe. Default: auto-detect from Windows SDK.

.PARAMETER TimestampServer
    RFC 3161 timestamp server URL. Default: http://timestamp.digicert.com.

.EXAMPLE
    .\build-release.ps1
    .\build-release.ps1 -SkipInstaller
    .\build-release.ps1 -CertificatePath .\cert.pfx -CertificatePassword $securePass
#>
param(
    [string]$Configuration = "Release",
    [switch]$SkipInstaller,
    [string]$OutputDir = "publish",
    [string]$CertificatePath = "",
    [string]$CertificatePassword = "",
    [string]$SignTool = "",
    [string]$TimestampServer = "http://timestamp.digicert.com"
)

Set-StrictMode -Version Latest
# Use "Continue" — native commands (dotnet, wix) write to stderr for warnings,
# and "Stop" would treat those as terminating errors in PowerShell 5.1.
$ErrorActionPreference = "Continue"

$Root = $PSScriptRoot
$AppProject = Join-Path $Root "src\Vido.App\Vido.App.csproj"
$SetupProject = Join-Path $Root "src\Vido.Setup\VidoSetup.csproj"
$PublishDir = Join-Path $Root $OutputDir
$PortableDir = Join-Path $PublishDir "portable"

# Signing setup
$SigningEnabled = $false
$ResolvedSignTool = ""
if ($CertificatePath -and (Test-Path $CertificatePath)) {
    $SigningEnabled = $true
    if ($SignTool -and (Test-Path $SignTool)) {
        $ResolvedSignTool = $SignTool
    }
    else {
        # Auto-detect signtool from Windows SDK
        $sdkPaths = @(
            "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe",
            "${env:ProgramFiles}\Windows Kits\10\bin\*\x64\signtool.exe"
        )
        foreach ($p in $sdkPaths) {
            $found = Get-Item $p -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
            if ($found) { $ResolvedSignTool = $found.FullName; break }
        }
        if (-not $ResolvedSignTool) {
            Write-Host "  WARNING: signtool.exe not found. Install Windows SDK or specify -SignTool path." -ForegroundColor Red
            $SigningEnabled = $false
        }
    }
}

function Invoke-CodeSign([string]$FilePath, [string]$Description) {
    if (-not $SigningEnabled) { return }
    Write-Host "  Signing: $(Split-Path $FilePath -Leaf)..." -ForegroundColor Gray
    $args = @(
        "sign", "/f", $CertificatePath,
        "/fd", "sha256",
        "/tr", $TimestampServer,
        "/td", "sha256",
        "/d", $Description
    )
    if ($CertificatePassword) {
        $args += @("/p", $CertificatePassword)
    }
    $args += $FilePath
    & $ResolvedSignTool @args
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  WARNING: Code signing failed for $(Split-Path $FilePath -Leaf)" -ForegroundColor Red
    }
}

# Read version from Directory.Build.props (single source of truth)
[xml]$buildProps = Get-Content (Join-Path $Root "Directory.Build.props")
$Version = $buildProps.SelectSingleNode('//VidoVersion').'#text'
if (-not $Version) { $Version = "0.1.0" }

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Vido $Version Release Build" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
if ($SigningEnabled) {
    Write-Host "  Code signing: ENABLED" -ForegroundColor Green
} else {
    Write-Host "  Code signing: DISABLED (no certificate)" -ForegroundColor Yellow
    Write-Host "  To sign, pass: -CertificatePath .\cert.pfx" -ForegroundColor Gray
}
Write-Host ""

# Step 1: Clean
Write-Host "[1/5] Cleaning previous output..." -ForegroundColor Yellow
if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }
New-Item -ItemType Directory -Force $PublishDir | Out-Null

# Step 2: Publish
Write-Host "[2/5] Publishing self-contained win-x64..." -ForegroundColor Yellow
dotnet publish $AppProject `
    -c $Configuration `
    -r win-x64 `
    --self-contained `
    -o $PortableDir `
    -p:PublishReadyToRun=true `
    -p:DebugType=none `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

# Sign the main EXE before zipping / packaging
Invoke-CodeSign (Join-Path $PortableDir "Vido.exe") "Vido Video Player"

# Step 3: Clean unnecessary files from portable output
Write-Host "[3/5] Cleaning publish output..." -ForegroundColor Yellow
Get-ChildItem $PortableDir -Filter "*.pdb" -Recurse | Remove-Item -Force
Get-ChildItem $PortableDir -Filter "*.xml" -Recurse | Where-Object { $_.Name -ne "plugin.json" } | Remove-Item -Force
Get-ChildItem $PortableDir -Filter "*.deps.dev.json" -Recurse | Remove-Item -Force

$files = Get-ChildItem $PortableDir -File -Recurse
$totalMB = [math]::Round(($files | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host "  Published $($files.Count) files, $totalMB MB total" -ForegroundColor Gray

# Step 4: Create Portable Zip
Write-Host "[4/5] Creating portable zip..." -ForegroundColor Yellow
$ZipName = "Vido-$Version-win-x64-portable.zip"
$ZipPath = Join-Path $PublishDir $ZipName
if (Test-Path $ZipPath) { Remove-Item $ZipPath }
Compress-Archive -Path "$PortableDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal
$zipSizeMB = [math]::Round((Get-Item $ZipPath).Length / 1MB, 1)
Write-Host "  Created: $ZipName - $zipSizeMB MB" -ForegroundColor Green

# Step 5: Build Custom Installer
if (-not $SkipInstaller) {
    Write-Host "[5/5] Building custom installer..." -ForegroundColor Yellow

    # Create payload zip from portable output
    $PayloadZip = Join-Path $PublishDir "payload.zip"
    if (Test-Path $PayloadZip) { Remove-Item $PayloadZip }
    Compress-Archive -Path "$PortableDir\*" -DestinationPath $PayloadZip -CompressionLevel Optimal

    # Build and publish Vido.Setup as single-file EXE
    $SetupOutput = Join-Path $PublishDir "setup"
    dotnet publish $SetupProject `
        -c $Configuration `
        -r win-x64 `
        --self-contained `
        -o $SetupOutput `
        "-p:PublishSingleFile=true" `
        "-p:PayloadZip=$PayloadZip"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  Installer build failed." -ForegroundColor Red
    }
    else {
        $SetupName = "VidoSetup-$Version.exe"
        $setupSource = Join-Path $SetupOutput "VidoSetup.exe"
        $SetupPath = Join-Path $PublishDir $SetupName
        Copy-Item $setupSource $SetupPath -Force

        Invoke-CodeSign $SetupPath "Vido Installer"

        $setupSizeMB = [math]::Round((Get-Item $SetupPath).Length / 1MB, 1)
        Write-Host "  Created: $SetupName - $setupSizeMB MB" -ForegroundColor Green
    }

    # Clean up intermediate files
    Remove-Item $PayloadZip -Force -ErrorAction SilentlyContinue
    Remove-Item $SetupOutput -Recurse -Force -ErrorAction SilentlyContinue
}
else {
    Write-Host "[5/5] Skipping installer build." -ForegroundColor Gray
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Build Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Output directory: $PublishDir" -ForegroundColor Gray
Get-ChildItem $PublishDir -File | ForEach-Object {
    $sizeMB = [math]::Round($_.Length / 1MB, 1)
    Write-Host "    $($_.Name)  $sizeMB MB" -ForegroundColor White
}
