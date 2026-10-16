@echo off
rem Double-click to play. Extra options are passed on, e.g.  play.bat -Games 300000
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0play.ps1" %*
if errorlevel 1 pause
