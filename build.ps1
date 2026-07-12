# TempMate one-click build script
# Produces a single-file, FRAMEWORK-DEPENDENT win-x64 executable (~few MB).
# Requires the .NET 5 Desktop Runtime to be installed on the target PC.
# (Your dev PC already has it via the .NET 5 SDK, so it runs here directly.)
#
# Usage (run in PowerShell from the project root):
#   powershell -ExecutionPolicy Bypass -File build.ps1
#
# What it does:
#   1. Kill any running TempMate.exe (prevents "file in use" publish errors)
#   2. Remove the old publish/ folder
#   3. dotnet publish -> single-file framework-dependent exe (no bundled runtime)
#   4. Print the output path
#
# 如需"任何电脑双击即跑"的自包含版本（体积较大，约 40-60MB），改用：
#   dotnet publish TempMate/TempMate.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish-standalone

$ErrorActionPreference = 'Continue'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $ScriptDir

Write-Host "=== TempMate build (single-file, win-x64, framework-dependent) ==="

# 1. Stop running instance to release the locked exe
$proc = Get-Process -Name "TempMate" -ErrorAction SilentlyContinue
if ($proc) {
    Write-Host "Stopping running TempMate instance (PID(s): $($proc.Id -join ','))..."
    $proc | Stop-Process -Force
    Start-Sleep -Seconds 1
}

# 2. Clean previous publish output
if (Test-Path "publish") {
    Write-Host "Cleaning old publish/ folder..."
    Remove-Item -Recurse -Force "publish"
}

# 3. Publish (framework-dependent: runtime NOT bundled -> much smaller exe)
Write-Host "Publishing (this may take a while on first run)..."
dotnet publish TempMate/TempMate.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false -o publish
if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAIL] dotnet publish exited with code $LASTEXITCODE"
    Pop-Location
    exit $LASTEXITCODE
}

# 4. Verify
$exe = Join-Path $ScriptDir "publish\TempMate.exe"
if (Test-Path $exe) {
    $mb = [math]::Round((Get-Item $exe).Length / 1MB, 2)
    Write-Host ""
    Write-Host "[OK] Build succeeded."
    Write-Host "     Output: $exe"
    Write-Host "     Size  : $mb MB"
    Write-Host "     Requires .NET 5 Desktop Runtime on the target PC."

    # 5. 附带启动器（缺失 .NET 5 时给出友好提示并打开下载页）
    $launcherSrc = Join-Path $ScriptDir "TempMate.Launcher.cmd"
    $launcherDst = Join-Path $ScriptDir "publish\TempMate.Launcher.cmd"
    if (Test-Path $launcherSrc) {
        Copy-Item -Force $launcherSrc $launcherDst
        Write-Host "     Launcher: $launcherDst  (用于分发给未装 .NET 5 的电脑)"
    }
} else {
    Write-Host "[FAIL] publish/TempMate.exe was not produced."
}

Pop-Location
