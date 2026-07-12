@echo off
setlocal EnableExtensions

REM TempMate launcher: check .NET 8 Desktop Runtime, then start TempMate.exe.
REM If missing, show a prompt with a link to the .NET 8 download page.

set "APP=%~dp0TempMate.exe"
set "FOUND=0"

if exist "%ProgramFiles%\dotnet\shared\Microsoft.WindowsDesktop.App" (
    for /d %%d in ("%ProgramFiles%\dotnet\shared\Microsoft.WindowsDesktop.App\8.0*") do set "FOUND=1"
)
if "%FOUND%"=="0" (
    if exist "%ProgramFiles(x86)%\dotnet\shared\Microsoft.WindowsDesktop.App" (
        for /d %%d in ("%ProgramFiles(x86)%\dotnet\shared\Microsoft.WindowsDesktop.App\8.0*") do set "FOUND=1"
    )
)

if "%FOUND%"=="1" goto :launch

reg query "HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\shared\Microsoft.WindowsDesktop.App" 2>nul | findstr /i "8.0" >nul
if not errorlevel 1 set "FOUND=1"
if "%FOUND%"=="1" goto :launch

goto :missing

:launch
if exist "%APP%" (
    start "" "%APP%"
) else (
    mshta "vbscript:MsgBox(""TempMate.exe was not found in the same folder as this launcher."",16,""Launch Failed"")"
)
goto :eof

:missing
mshta "vbscript:if MsgBox(""TempMate requires .NET 8 Desktop Runtime.""&vbCrLf&vbCrLf&""Open the download page now?"",36,""Missing Runtime"")=6 then CreateObject(""WScript.Shell"").Run(""https://dotnet.microsoft.com/download/dotnet/8.0"")"
goto :eof
