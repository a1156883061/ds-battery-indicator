@echo off
echo === 发布 DS Battery Indicator ===
cd /d %~dp0DsBatteryIndicator
rmdir /s /q ..\publish 2>nul
dotnet publish -c Release -o ..\publish
echo.
echo 发布完成！产物在 publish\ 目录
echo 用户需安装 .NET 8 Desktop Runtime 才能运行
pause
