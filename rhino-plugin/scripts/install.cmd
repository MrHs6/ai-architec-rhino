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
"%SystemRoot%\System32\tasklist.exe" /FI "IMAGENAME eq Rhino.exe" /NH | "%SystemRoot%\System32\findstr.exe" /I /C:"Rhino.exe" >nul
if not errorlevel 1 (
  echo Save your work and close every Rhino window before installing.
  echo A running prototype can overwrite its registration again at shutdown.
  exit /b 1
)
"%ARCHITEC_RHINO_SYSTEM%\Yak.exe" install "%ARCHITEC_PACKAGE%"
if errorlevel 1 exit /b 1
if not exist "%~dp0repair-registration.cmd" (
  echo Registration repair script is missing. Extract every file from the installer ZIP.
  exit /b 1
)
call "%~dp0repair-registration.cmd" "%ARCHITEC_PACKAGE%"
if errorlevel 1 (
  echo Yak installed the package, but registration repair could not finish.
  echo Run repair-registration.cmd with the bundled .yak path and check its error before restarting Rhino.
  exit /b 1
)
echo Installed. Save your work, restart Rhino, then type archtec and press Enter.
exit /b 0
