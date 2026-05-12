@echo off
chcp 65001 >nul
echo === Publishing DS Battery Indicator ===
cd /d "%~dp0DsBatteryIndicator"
if exist "..\publish" rmdir /s /q "..\publish"
dotnet publish -c Release -o ..\publish
echo.
echo Published to publish\ directory
echo Users need .NET 8 Desktop Runtime to run
echo.
pause
