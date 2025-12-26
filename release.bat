@echo off
REM Panomi Release Build Script
REM Usage: release.bat [version]
REM Example: release.bat 1.0.0

setlocal

set VERSION=%1
if "%VERSION%"=="" (
    echo Usage: release.bat [version]
    echo Example: release.bat 1.0.0
    exit /b 1
)

echo.
echo ========================================
echo  Building Panomi v%VERSION%
echo ========================================
echo.

REM Clean previous builds
echo [1/4] Cleaning previous builds...
if exist "release" rmdir /s /q "release"
if exist "publish" rmdir /s /q "publish"

REM Publish the app
echo [2/4] Publishing application...
dotnet publish src/Panomi.UI/Panomi.UI.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained -o "./publish"

if %ERRORLEVEL% neq 0 (
    echo ERROR: Publish failed!
    exit /b 1
)

REM Package with Velopack
echo [3/4] Creating Velopack installer...
vpk pack --packId Panomi --packVersion %VERSION% --packDir ./publish --mainExe Panomi.UI.exe --outputDir ./release --icon src/Panomi.UI/Assets/logo_ico.ico

if %ERRORLEVEL% neq 0 (
    echo ERROR: Velopack packaging failed!
    echo Make sure vpk is installed: dotnet tool install -g vpk
    exit /b 1
)

echo.
echo ========================================
echo  Build Complete!
echo ========================================
echo.
echo Release files in: release/
echo.
echo Upload these files to GitHub Releases:
dir /b release\
echo.
echo Done!
