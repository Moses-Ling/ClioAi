@echo off
setlocal

REM clean.cmd - Remove local build artifacts and logs (safe cleanup)
REM Usage:
REM   clean.cmd            (cleans bin/ obj/ .vs/ tests/bin tests/obj and logs)

echo Cleaning build artifacts and logs...

for %%D in ("bin" "obj" ".vs" "tests\bin" "tests\obj") do (
  if exist %%D (
    echo Removing directory: %%D
    rmdir /S /Q "%%D" 2> NUL
  )
)

for %%F in ("*.log" "*log,Verbosity") do (
  for /f "delims=" %%P in ('dir /b /a:-d %%F 2^>NUL') do (
    echo Deleting log: %%P
    del /F /Q "%%P" 2> NUL
  )
)

echo Done.
exit /b 0

