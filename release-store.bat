@echo off
REM Panomi Microsoft Store Build Script
REM Usage: release-store.bat [version]
REM Example: release-store.bat 1.4.2
REM
REM Outputs MSIX package for upload to Microsoft Partner Center

setlocal

set VERSION=%1
if "%VERSION%"=="" (
    echo Usage: release-store.bat [version]
    echo Example: release-store.bat 1.4.2
    exit /b 1
)

echo.
echo ========================================
echo  Building Panomi v%VERSION% for Store
echo ========================================
echo.

REM Clean previous builds
echo [1/3] Cleaning previous builds...
if exist "store-publish" rmdir /s /q "store-publish"

REM Publish for Store (uses Store configuration, no Velopack)
echo [2/3] Building MSIX for Microsoft Store...
dotnet publish src/Panomi.UI/Panomi.UI.csproj -c Store -p:Platform=x64 -p:RuntimeIdentifier=win-x64

if %ERRORLEVEL% neq 0 (
    echo ERROR: Publish failed!
    exit /b 1
)

echo.
echo [3/3] Build complete!
echo.
echo ========================================
echo  Store Build Complete!
echo ========================================
echo.
echo MSIX package location:
echo src\Panomi.UI\bin\x64\Store\net8.0-windows10.0.19041.0\AppPackages\
echo.
echo Next steps:
echo 1. Go to Partner Center: https://partner.microsoft.com
echo 2. Apps and games ^> New product ^> App
echo 3. Upload the .msix file from the AppPackages folder
echo.
echo Done!
