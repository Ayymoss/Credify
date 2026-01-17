@echo off
setlocal enabledelayedexpansion

:: Parse game type argument or prompt user
set GAME_TYPE=%1
if "%GAME_TYPE%"=="" (
    echo.
    echo Select game type:
    echo   1 - Poker
    echo   2 - Blackjack
    echo   3 - Roulette
    echo.
    set /p GAME_CHOICE="Enter choice (1/2/3): "
    
    if "!GAME_CHOICE!"=="1" set GAME_TYPE=Poker
    if "!GAME_CHOICE!"=="2" set GAME_TYPE=Blackjack
    if "!GAME_CHOICE!"=="3" set GAME_TYPE=Roulette
    
    if not defined GAME_TYPE (
        echo Invalid choice. Defaulting to Poker.
        set GAME_TYPE=Poker
    )
)

if /i "%GAME_TYPE%"=="poker" set GAME_TYPE=Poker
if /i "%GAME_TYPE%"=="blackjack" set GAME_TYPE=Blackjack
if /i "%GAME_TYPE%"=="roulette" set GAME_TYPE=Roulette

title Launch %GAME_TYPE% Test Harness
echo Starting %GAME_TYPE% Test Harness...
echo.

:: Start GameServer in new window
start "%GAME_TYPE% Server" cmd /k "dotnet run --project GameServer\GameServer.csproj -- --game %GAME_TYPE%"

:: Wait for server to start
timeout /t 3 /nobreak > nul

:: Start Player 1
start "Player 1" cmd /k "dotnet run --project PlayerClient\PlayerClient.csproj -- --name Player1 --game %GAME_TYPE%"

:: Start Player 2
start "Player 2" cmd /k "dotnet run --project PlayerClient\PlayerClient.csproj -- --name Player2 --game %GAME_TYPE%"

echo.
echo All 3 windows launched!
echo - Server window: Shows all game traffic (debug monitor)
echo - Player 1 window: Type /join to join, then play!
echo - Player 2 window: Type /join to join, then play!
echo.
echo Usage: LaunchAll.bat [poker^|blackjack^|roulette]
echo       If no argument provided, you will be prompted to select a game
echo.
pause
