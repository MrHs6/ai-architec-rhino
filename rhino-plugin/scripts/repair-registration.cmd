@echo off
setlocal EnableExtensions DisableDelayedExpansion
rem Native CMD companion: keeps installation usable when PowerShell scripts are restricted.
if "%~1"=="" exit /b 1
for %%F in ("%~1") do set "ARCHITEC_STEM=%%~nF"
set "ARCHITEC_VERSION=%ARCHITEC_STEM:ai-architec-rhino-=%"
set "ARCHITEC_VERSION=%ARCHITEC_VERSION:-rh8_31-win=%"
rem Validate before using the filename-derived version in commands or paths.
set ARCHITEC_VERSION | "%SystemRoot%\System32\findstr.exe" /I /R /X "ARCHITEC_VERSION=[0-9][0-9.abcdefghijklmnopqrstuvwxyz-]*" >nul
if errorlevel 1 exit /b 1
if /I not "%ARCHITEC_STEM%"=="ai-architec-rhino-%ARCHITEC_VERSION%-rh8_31-win" exit /b 1
set "ARCHITEC_DIRECTORY=%APPDATA%\McNeel\Rhinoceros\packages\8.0\ai-architec-rhino\%ARCHITEC_VERSION%"
set "ARCHITEC_TARGET=%ARCHITEC_DIRECTORY%\AIArchiTec.rhp"
if not exist "%ARCHITEC_TARGET%" exit /b 1
if not exist "%ARCHITEC_DIRECTORY%\AIArchiTec.Contracts.dll" exit /b 1
"%SystemRoot%\System32\findstr.exe" /X /C:"name: ai-architec-rhino" "%ARCHITEC_DIRECTORY%\manifest.yml" >nul
if errorlevel 1 exit /b 1
"%SystemRoot%\System32\findstr.exe" /X /C:"version: %ARCHITEC_VERSION%" "%ARCHITEC_DIRECTORY%\manifest.yml" >nul
if errorlevel 1 exit /b 1
set "ARCHITEC_KEY=HKCU\Software\McNeel\Rhinoceros\8.0\Plug-Ins\0ab9a769-52f9-4378-85cb-cf8177ed5166"
"%SystemRoot%\System32\tasklist.exe" /FI "IMAGENAME eq Rhino.exe" /NH | "%SystemRoot%\System32\findstr.exe" /I /C:"Rhino.exe" >nul
if not errorlevel 1 (
  echo Close every Rhino window before repairing registration.
  exit /b 1
)
set "ARCHITEC_REG=%SystemRoot%\System32\reg.exe"
"%ARCHITEC_REG%" query "%ARCHITEC_KEY%" >nul 2>&1
if errorlevel 1 (
  echo No legacy registration found. Rhino will discover the Yak package on startup.
  exit /b 0
)
set "ARCHITEC_PREVIOUS="
for /f "tokens=2,*" %%A in ('call "%ARCHITEC_REG%" query "%ARCHITEC_KEY%\PlugIn" /v FileName 2^>nul') do if /I "%%A"=="REG_SZ" set "ARCHITEC_PREVIOUS=%%B"
if /I "%ARCHITEC_PREVIOUS%"=="%ARCHITEC_TARGET%" (
  "%ARCHITEC_REG%" query "%ARCHITEC_KEY%\CommandList" /v archtec >nul 2>&1
  if not errorlevel 1 (
    echo Plugin registration already targets this Studio package.
    exit /b 0
  )
)
set "ARCHITEC_BACKUP_DIRECTORY=%LOCALAPPDATA%\AIArchiTec\InstallationBackups"
if not exist "%ARCHITEC_BACKUP_DIRECTORY%" mkdir "%ARCHITEC_BACKUP_DIRECTORY%"
if not exist "%ARCHITEC_BACKUP_DIRECTORY%" exit /b 1
:backupname
set "ARCHITEC_BACKUP=%ARCHITEC_BACKUP_DIRECTORY%\Rhino8-cmd-%RANDOM%-%RANDOM%.reg"
if exist "%ARCHITEC_BACKUP%" goto backupname
"%ARCHITEC_REG%" export "%ARCHITEC_KEY%" "%ARCHITEC_BACKUP%" /y >nul
if errorlevel 1 exit /b 1
"%ARCHITEC_REG%" add "%ARCHITEC_KEY%\PlugIn" /v FileName /t REG_SZ /d "%ARCHITEC_TARGET%" /f >nul
if errorlevel 1 goto rollback
"%ARCHITEC_REG%" add "%ARCHITEC_KEY%\CommandList" /v archtec /t REG_SZ /d "2;archtec" /f >nul
if errorlevel 1 goto rollback
echo Registration repaired. Backup: "%ARCHITEC_BACKUP%"
echo Save your work and restart Rhino, then run archtec.
exit /b 0
:rollback
"%ARCHITEC_REG%" import "%ARCHITEC_BACKUP%" >nul
echo Registration repair failed. Rollback attempted; backup: "%ARCHITEC_BACKUP%"
exit /b 1
