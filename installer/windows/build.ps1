param(
    [string]$Version = "0.12.3"
)

$ErrorActionPreference = "Stop"

$Root = (Resolve-Path "$PSScriptRoot\..\..").Path
$Dist = Join-Path $Root "dist"
$Work = Join-Path $Dist "work\windows"
$AppWork = Join-Path $Work "app"
$SetupExe = Join-Path $Dist "Hearthlens-Setup-$Version-win-x64.exe"
$PayloadDir = Join-Path $Root "src\Hearthlens.Installer\payload"
$PayloadZip = Join-Path $PayloadDir "app.zip"

Write-Host "==========================================================" -ForegroundColor DarkGreen
Write-Host " Building Hearthlens $Version Custom Installer (Windows)" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor DarkGreen

# 1. Clean directories
Write-Host "`n[1/4] Cleaning build workspace..." -ForegroundColor Cyan
if (Test-Path $Work) { Remove-Item $Work -Recurse -Force }
New-Item -ItemType Directory -Path $Work -Force | Out-Null
New-Item -ItemType Directory -Path $Dist -Force | Out-Null
New-Item -ItemType Directory -Path $PayloadDir -Force | Out-Null

# 2. Publish Windows App
Write-Host "[2/4] Publishing Hearthlens application (net10.0-windows, win-x64)..." -ForegroundColor Cyan
dotnet publish "$Root\src\Hearthlens.App\Hearthlens.App.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:EnableWindowsTargeting=true -p:Version="$Version" `
    -o $AppWork -nologo -v q

# 3. Create embedded payload zip
Write-Host "[3/4] Compressing application payload into installer..." -ForegroundColor Cyan
if (Test-Path $PayloadZip) { Remove-Item $PayloadZip -Force }
Compress-Archive -Path "$AppWork\*" -DestinationPath $PayloadZip -Force

# 4. Build single-file custom UI installer
Write-Host "[4/4] Compiling custom WPF Setup wizard (single-file executable)..." -ForegroundColor Cyan
$InstallerPublishDir = Join-Path $Work "installer-out"
dotnet publish "$Root\src\Hearthlens.Installer\Hearthlens.Installer.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:Version="$Version" `
    -o $InstallerPublishDir -nologo -v q

$PublishedSetup = Get-ChildItem -Path $InstallerPublishDir -Filter "Hearthlens-Setup-*.exe" | Select-Object -First 1
if ($PublishedSetup) {
    Get-Process -Name "*Hearthlens-Setup*" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 300
    Copy-Item $PublishedSetup.FullName $SetupExe -Force
    $fileItem = Get-Item $SetupExe
    $sizeMb = [math]::Round($fileItem.Length / 1MB, 2)
    Write-Host "`n[SUCCESS] Generated custom installer:" -ForegroundColor Green
    Write-Host "  -> $SetupExe ($($sizeMb) MB)`n" -ForegroundColor Yellow
} else {
    throw "Installer executable was not found in $InstallerPublishDir"
}
