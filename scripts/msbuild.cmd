@echo off
setlocal ENABLEDELAYEDEXPANSION

REM msbuild.cmd - Finds and invokes MSBuild.exe reliably.
REM Priority:
REM  1) MSBUILD_EXE_PATH env var
REM  2) vswhere (latest with Microsoft.Component.MSBuild)
REM  3) Common known install paths (VS 2022 Community/BuildTools, VS 2019 BuildTools)

set "MSBUILD="

REM 1) Honor explicit env var if set
if defined MSBUILD_EXE_PATH (
  if exist "%MSBUILD_EXE_PATH%" (
    set "MSBUILD=%MSBUILD_EXE_PATH%"
  )
)

REM 2) Try vswhere to locate latest VS with MSBuild
if not defined MSBUILD (
  set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
  if exist "%VSWHERE%" (
    for /f "usebackq delims=" %%I in (`"%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -property installationPath`) do set "VSINSTALL=%%I"
    if defined VSINSTALL (
      if exist "%VSINSTALL%\MSBuild\Current\Bin\MSBuild.exe" set "MSBUILD=%VSINSTALL%\MSBuild\Current\Bin\MSBuild.exe"
    )
  )
)

REM 3) Fallback to common paths
if not defined MSBUILD (
  for %%P in (
    "C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\MSBuild\\Current\\Bin\\MSBuild.exe"
    "C:\\Program Files\\Microsoft Visual Studio\\2022\\BuildTools\\MSBuild\\Current\\Bin\\MSBuild.exe"
    "C:\\Program Files (x86)\\Microsoft Visual Studio\\2022\\BuildTools\\MSBuild\\Current\\Bin\\MSBuild.exe"
    "C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\BuildTools\\MSBuild\\Current\\Bin\\MSBuild.exe"
  ) do (
    if exist %%~P (
      set "MSBUILD=%%~P"
      goto :found
    )
  )
)

:found
if not defined MSBUILD (
  echo ERROR: Could not locate MSBuild.exe. 1>&2
  echo Tried MSBUILD_EXE_PATH, vswhere, and common install paths. 1>&2
  exit /b 1
)

"%MSBUILD%" %*
exit /b %ERRORLEVEL%

