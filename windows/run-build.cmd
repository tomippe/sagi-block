@echo off
cd /d "%~dp0"
echo [%date% %time%] Stopping SagiBlock...>> "%~dp0last-build.log"
taskkill /IM SagiBlock.exe /F >> "%~dp0last-build.log" 2>&1
timeout /t 1 /nobreak >nul
echo [%date% %time%] Building...>> "%~dp0last-build.log"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" >> "%~dp0last-build.log" 2>&1
set EC=%ERRORLEVEL%
echo EXIT=%EC%>> "%~dp0last-build.log"
exit /b %EC%

