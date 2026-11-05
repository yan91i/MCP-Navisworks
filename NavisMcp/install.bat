@echo off
REM Per-user install (no admin). Usage: install.bat [version]   default 2024
REM -> %APPDATA%\Autodesk Navisworks Manage <ver>\Plugins\NavisMcp\NavisMcp.dll
setlocal
cd /d "%~dp0"
set "VER=2024"
if not "%~1"=="" set "VER=%~1"
if not exist bin\NavisMcp.dll (
  echo [ERROR] bin\NavisMcp.dll not found. Run build.bat first.
  exit /b 1
)
set "DEST=%APPDATA%\Autodesk Navisworks Manage %VER%\Plugins\NavisMcp"
if not exist "%DEST%" mkdir "%DEST%"
copy /y bin\NavisMcp.dll "%DEST%\NavisMcp.dll" >nul
if errorlevel 1 (
  echo [ERROR] Copy failed. Is Navisworks still running?
  exit /b 1
)
echo [OK] Installed to "%DEST%"
echo Start Navisworks, then open http://127.0.0.1:3900/tools in a browser.
endlocal
