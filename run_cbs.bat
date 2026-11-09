@echo off
chcp 65001 >nul
title S7 CBS — Core Banking System (Bangladesh Bank BRPD Circular 12 v2.0)
cd /d "%~dp0"

echo ===============================================================================
echo   S7 CBS — Core Banking System (Bangladesh Bank BRPD Circular 12 v2.0)
echo   Theme: Dark & Light Mode  ^|  Language: English & Bengali
echo ===============================================================================
echo.
echo [*] Checking .NET environment...
where dotnet >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [!] ERROR: .NET SDK is not installed or not in PATH.
    echo     Please install .NET 9 SDK from https://dotnet.microsoft.com/download
    echo.
    pause
    exit /b 1
)

echo [*] Launching CBS Web Server at: http://localhost:5000
echo [*] Opening application in your default web browser...
echo.

:: Open default browser after 2 seconds to allow server initialization
start "" cmd /c "timeout /t 2 /nobreak >nul && start http://localhost:5000"

:: Run ASP.NET Core Web Host with Static Files & APIs
dotnet run --project src\CBS.Api --urls "http://localhost:5000"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [!] Application stopped with error code %ERRORLEVEL%.
    pause
)
