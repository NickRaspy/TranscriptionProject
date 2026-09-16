@echo off
setlocal
set "APPDATA=%~dp0.appdata"
if not exist "%APPDATA%" mkdir "%APPDATA%"
pushd "%~dp0"
dotnet test TranscriptMvp.sln -c Release --no-restore %*
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%
