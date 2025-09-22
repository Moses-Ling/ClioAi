@echo off
setlocal ENABLEDELAYEDEXPANSION

REM build.cmd - Convenience wrapper for building the solution.
REM Usage:
REM   build.cmd [Configuration] [Clean]
REM Examples:
REM   build.cmd            (Debug Build)
REM   build.cmd Release    (Release Build)
REM   build.cmd Debug Clean (Clean+Build Debug)

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Debug"

set "TARGET=Build"
if /I "%~2"=="Clean" set "TARGET=Clean;Build"

set "SCRIPT_DIR=%~dp0"
set "ROOT=%SCRIPT_DIR%..\"

if not exist "%SCRIPT_DIR%msbuild.cmd" (
  echo ERROR: Required script not found: %SCRIPT_DIR%msbuild.cmd 1>&2
  exit /b 1
)

echo Building %CONFIG% (^/t:%TARGET%^) ...
call "%SCRIPT_DIR%msbuild.cmd" "%ROOT%AudioTranscriptionApp.sln" /t:%TARGET% /p:Configuration=%CONFIG% /m /v:m
exit /b %ERRORLEVEL%

