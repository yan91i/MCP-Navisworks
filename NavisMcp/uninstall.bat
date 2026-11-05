@echo off
set "VER=2024"
if not "%~1"=="" set "VER=%~1"
rmdir /s /q "%APPDATA%\Autodesk Navisworks Manage %VER%\Plugins\NavisMcp"
echo Removed.
