@echo off
setlocal
rem v1.1 supply2 + 79 ten new coin-only character skins (5 per job, 1000 drink-sale coins each) (2026-10-20 skins10). Same ISOLATED v1.1 test save as Play-DrinkLab-v1.1-MVP.cmd.
rem Never touches the real save or the store 1.0.x build. The old v1.1, hudfix, supply and supply2 build folders are left as is.
set "CCT_BUILD=%~dp0..\..\Builds\Playable-2026-10-20-drinklab-v1.1-skins10"
set "CCT_DATA=%~dp0TestData\drinklab-v1.1"
if not exist "%CCT_BUILD%\CocktailCoffeeTea.exe" (
  echo skins10 build is missing: %CCT_BUILD%
  pause
  exit /b 1
)
rem Two game windows must not share one save: if any CocktailCoffeeTea is already running, ask to close it first (nothing is closed automatically).
tasklist /FI "IMAGENAME eq CocktailCoffeeTea.exe" 2>nul | find /I "CocktailCoffeeTea.exe" >nul
if not errorlevel 1 (
  echo CocktailCoffeeTea is already running. Close the running game first, then start this launcher again.
  pause
  exit /b 2
)
if not exist "%CCT_DATA%" mkdir "%CCT_DATA%"
start "" /D "%CCT_BUILD%" "%CCT_BUILD%\CocktailCoffeeTea.exe" -cct-data-dir "%CCT_DATA%" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 %*
