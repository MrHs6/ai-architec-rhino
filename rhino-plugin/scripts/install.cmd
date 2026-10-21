@echo off
setlocal EnableExtensions DisableDelayedExpansion
if not defined ARCHITEC_RHINO_SYSTEM set "ARCHITEC_RHINO_SYSTEM=%ProgramFiles%\Rhino 8\System"
if not exist "%ARCHITEC_RHINO_SYSTEM%\Yak.exe" (
  echo Rhino 8 was not found. Set ARCHITEC_RHINO_SYSTEM to its System directory.
  exit /b 1
)
set "ARCHITEC_PACKAGE="
if /I "%~1"=="/check" goto discover
if not "%~1"=="" (
  if /I not "%~x1"==".yak" (
    echo Supply a local .yak path, /check, or no arguments for the bundled package.
    exit /b 1
  )
  set "ARCHITEC_PACKAGE=%~f1"
  goto ready
)
:discover
set /a ARCHITEC_PACKAGE_COUNT=0 >nul
for %%F in ("%~dp0ai-architec-rhino-*-rh8_31-win.yak") do if exist "%%~fF" (
  set /a ARCHITEC_PACKAGE_COUNT+=1 >nul
  set "ARCHITEC_PACKAGE=%%~fF"
)
if not "%ARCHITEC_PACKAGE_COUNT%"=="1" (
  echo Extract the installer ZIP, or supply exactly one local .yak path.
  exit /b 1
)
:ready
if not exist "%ARCHITEC_PACKAGE%" (
  echo Package file was not found.
  exit /b 1
)
if /I "%~1"=="/check" (
  echo Yak and bundled package paths verified. No installation performed.
  exit /b 0
)
"%ARCHITEC_RHINO_SYSTEM%\Yak.exe" install "%ARCHITEC_PACKAGE%"
if errorlevel 1 exit /b 1
echo Installed. Save your work, restart Rhino, then type archtec and press Enter.
exit /b 0
