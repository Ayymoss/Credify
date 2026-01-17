@echo off
title Launch Poker Test Harness
echo Starting Poker Test Harness...
echo.

:: Start GameServer in new window
start "Poker Server" cmd /k "dotnet run --project GameServer\GameServer.csproj"

:: Wait for server to start
timeout /t 3 /nobreak > nul

:: Start Player 1
start "Player 1" cmd /k "dotnet run --project PlayerClient\PlayerClient.csproj -- --name Player1"

:: Start Player 2
start "Player 2" cmd /k "dotnet run --project PlayerClient\PlayerClient.csproj -- --name Player2"

echo.
echo All 3 windows launched!
echo - Server window: Shows all game traffic (debug monitor)
echo - Player 1 window: Type /join to join, then play!
echo - Player 2 window: Type /join to join, then play!
echo.
pause
