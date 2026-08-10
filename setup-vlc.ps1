<#
.SYNOPSIS
    Setup LibVLC native binaries for MMRC Player
.DESCRIPTION
    Copies LibVLC from installed VLC, NuGet cache, or downloads from videolan.org
#>

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$LibVLCDir = Join-Path $ScriptDir "libvlc-native"

Write-Host "LibVLC Native Setup" -ForegroundColor Cyan

# Skip if already exists
$checkFile = Join-Path $LibVLCDir "libvlccore.dll"
if (Test-Path $checkFile) {
    $size = [math]::Round((Get-Item $checkFile).Length / 1KB, 0)
    Write-Host "  Already exists ($size KB). OK." -ForegroundColor Green
    exit 0
}

New-Item -ItemType Directory -Path $LibVLCDir -Force | Out-Null

# Source 1: Installed VLC
$vlcPaths = @(
    "C:\Program Files\VideoLAN\VLC",
    "${env:ProgramFiles}\VideoLAN\VLC"
)

$copied = $false
foreach ($vlcPath in $vlcPaths) {
    $coreDll = Join-Path $vlcPath "libvlccore.dll"
    if (Test-Path $coreDll) {
        Write-Host "  Found installed VLC: $vlcPath" -ForegroundColor Green

        # Core DLLs
        @("libvlccore.dll", "libvlc.dll") | ForEach-Object {
            $src = Join-Path $vlcPath $_
            if (Test-Path $src) {
                Copy-Item $src -Destination $LibVLCDir -Force
                $size = [math]::Round((Get-Item $src).Length / 1KB, 0)
                Write-Host "    $_ ($size KB)" -ForegroundColor Green
            }
        }

        # Plugins
        $pluginsSrc = Join-Path $vlcPath "plugins"
        if (Test-Path $pluginsSrc) {
            $destPlugins = Join-Path $LibVLCDir "plugins"
            Copy-Item -Path $pluginsSrc -Destination $destPlugins -Recurse -Force
            $cnt = (Get-ChildItem -Path $destPlugins -Recurse -Filter "*.dll").Count
            $sz = [math]::Round((Get-ChildItem -Path $destPlugins -Recurse -Filter "*.dll" | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
            Write-Host "    plugins/ ($cnt DLLs, $sz MB)" -ForegroundColor Green
        }

        $copied = $true
        break
    }
}

# Source 2: NuGet cache fallback
if (-not $copied) {
    Write-Host "`n  Installed VLC not found. Trying NuGet cache..." -ForegroundColor Yellow

    $nugetRoots = @()
    if ($env:NUGET_PACKAGES) {
        $nugetRoots += $env:NUGET_PACKAGES
    }
    $nugetRoots += (Join-Path $env:USERPROFILE ".nuget\packages")

    $vlcPkg = $null
    foreach ($nugetRoot in ($nugetRoots | Select-Object -Unique)) {
        $packageRoot = Join-Path $nugetRoot "videolan.libvlc.windows"
        if (Test-Path $packageRoot) {
            $vlcPkg = Get-ChildItem -Path $packageRoot -Directory -ErrorAction SilentlyContinue |
                Sort-Object Name -Descending | Select-Object -First 1
            if ($vlcPkg) { break }
        }
    }

    if ($vlcPkg) {
            Write-Host "    Package: $($vlcPkg.Name)" -ForegroundColor Gray
            $nativeDir = Join-Path $vlcPkg.FullName "build\x64"
            if (-not (Test-Path $nativeDir)) {
                Write-Host "    x64 native directory not found: $nativeDir" -ForegroundColor Red
                exit 1
            }

            $allDlls = Get-ChildItem -Path $nativeDir -Recurse -Filter "*.dll"

            foreach ($dll in $allDlls) {
                if ($dll.Name -in @("libvlccore.dll", "libvlc.dll")) {
                    Copy-Item $dll.FullName -Destination $LibVLCDir -Force
                    Write-Host "    $($dll.Name) from $($dll.DirectoryName)" -ForegroundColor Green
                }
            }

            $pluginsDirs = Get-ChildItem -Path $nativeDir -Recurse -Directory -Filter "plugins"
            foreach ($pDir in $pluginsDirs) {
                $pluginDlls = Get-ChildItem -Path $pDir.FullName -Filter "*.dll"
                if ($pluginDlls.Count -gt 0) {
                    $destPlugins = Join-Path $LibVLCDir "plugins"
                    New-Item -ItemType Directory -Path $destPlugins -Force | Out-Null
                    Copy-Item -Path "$($pDir.FullName)\*.dll" -Destination $destPlugins -Force
                    Write-Host "    plugins/ ($($pluginDlls.Count) DLLs)" -ForegroundColor Green
                    break
                }
            }

            $copied = (Test-Path (Join-Path $LibVLCDir "libvlc.dll")) -and
                      (Test-Path (Join-Path $LibVLCDir "plugins"))
    }
}

# Source 3: Download VLC from videolan.org
if (-not $copied) {
    Write-Host "`n  NuGet cache incomplete. Downloading VLC 3.0.20..." -ForegroundColor Yellow

    $vlcVersion = "3.0.20"
    $vlcUrl = "https://get.videolan.org/vlc/$vlcVersion/win64/vlc-$vlcVersion-win64.zip"
    $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "vlc-download-$(Get-Random)"
    $zipFile = Join-Path $tempDir "vlc.zip"

    try {
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

        Write-Host "    Downloading $vlcUrl" -ForegroundColor Gray
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $vlcUrl -OutFile $zipFile -UseBasicParsing

        Write-Host "    Extracting..." -ForegroundColor Gray
        Expand-Archive -Path $zipFile -DestinationPath $tempDir -Force

        $vlcExtracted = Join-Path $tempDir "vlc-$vlcVersion"
        if (-not (Test-Path $vlcExtracted)) {
            $vlcExtracted = Get-ChildItem -Path $tempDir -Directory -Filter "vlc-*" | Select-Object -First 1
            if ($vlcExtracted) { $vlcExtracted = $vlcExtracted.FullName }
        }

        if (-not $vlcExtracted -or -not (Test-Path $vlcExtracted)) {
            Write-Host "    Extracted VLC folder not found" -ForegroundColor Red
            exit 1
        }

        foreach ($dll in @("libvlccore.dll", "libvlc.dll")) {
            $src = Join-Path $vlcExtracted $dll
            if (Test-Path $src) {
                Copy-Item $src -Destination $LibVLCDir -Force
                $size = [math]::Round((Get-Item $src).Length / 1KB, 0)
                Write-Host "    $dll ($size KB)" -ForegroundColor Green
            }
        }

        $pluginsSrc = Join-Path $vlcExtracted "plugins"
        if (Test-Path $pluginsSrc) {
            $destPlugins = Join-Path $LibVLCDir "plugins"
            Copy-Item -Path $pluginsSrc -Destination $destPlugins -Recurse -Force
            $cnt = (Get-ChildItem -Path $destPlugins -Recurse -Filter "*.dll").Count
            $sz = [math]::Round((Get-ChildItem -Path $destPlugins -Recurse -Filter "*.dll" | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
            Write-Host "    plugins/ ($cnt DLLs, $sz MB)" -ForegroundColor Green
        }

        $copied = (Test-Path (Join-Path $LibVLCDir "libvlc.dll"))
    }
    finally {
        if (Test-Path $tempDir) { Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

# Verify
if (-not (Test-Path (Join-Path $LibVLCDir "libvlccore.dll")) -or
    -not (Test-Path (Join-Path $LibVLCDir "libvlc.dll")) -or
    -not (Test-Path (Join-Path $LibVLCDir "plugins"))) {
    Write-Host "`nERROR: Could not find LibVLC" -ForegroundColor Red
    Write-Host "  Restore VideoLAN.LibVLC.Windows or install x64 VLC from https://www.videolan.org/vlc/" -ForegroundColor Yellow
    exit 1
}

$totalMB = [math]::Round((Get-ChildItem -Path $LibVLCDir -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
Write-Host "`n  Ready: $totalMB MB" -ForegroundColor Green