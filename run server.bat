@echo off
title ElshazlyStore API Server
echo Checking for existing server on port 5238...
for /f "tokens=5" %%a in ('netstat -aon ^| findstr ":5238" ^| findstr "LISTENING"') do (
    echo Killing existing process PID %%a ...
    taskkill /PID %%a /F >nul 2>&1
)
echo Starting ElshazlyStore API...
cd /d "%~dp0src\ElshazlyStore.Api"
dotnet run
pause
