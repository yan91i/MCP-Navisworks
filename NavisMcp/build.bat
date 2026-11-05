@echo off
REM ============================================================
REM  Build NavisMcp.dll with the built-in .NET Framework csc.exe
REM  No admin rights / no Visual Studio needed.
REM  Usage:  build.bat
REM          build.bat "C:\Program Files\Autodesk\Navisworks Manage 2024"
REM ============================================================
setlocal
cd /d "%~dp0"

set "NW=D:\Users\LI_Y1\Program Files\Autodesk\Navisworks Manage 2024"
if not "%~1"=="" set "NW=%~1"

if not exist "%NW%\Autodesk.Navisworks.Api.dll" (
  echo [ERROR] Autodesk.Navisworks.Api.dll not found in "%NW%"
  echo         Run: build.bat "your Navisworks install folder"
  exit /b 1
)

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "FX=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"

if not exist bin mkdir bin

"%CSC%" /nologo /target:library /platform:x64 /optimize+ /nowarn:1701,1702,0168,0219 ^
  /out:bin\NavisMcp.dll ^
  /r:"%NW%\Autodesk.Navisworks.Api.dll" ^
  /r:"%NW%\Autodesk.Navisworks.Clash.dll" ^
  /r:"%NW%\Autodesk.Navisworks.ComApi.dll" ^
  /r:"%NW%\Autodesk.Navisworks.Interop.ComApi.dll" ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll ^
  /r:"%FX%\Facades\netstandard.dll" ^
  src\*.cs > build.log 2>&1

type build.log
findstr /c:"error CS" build.log >nul
if not errorlevel 1 (
  echo.
  echo [BUILD FAILED] see build.log - send it back for a fix
  exit /b 1
)
echo.
echo [OK] bin\NavisMcp.dll built. Close Navisworks, then run install.bat
endlocal
