@echo off
setlocal EnableExtensions DisableDelayedExpansion
set "ARCHITEC_RHINO_MAJOR=9"
call "%~dp0install.cmd" %*
exit /b %errorlevel%
