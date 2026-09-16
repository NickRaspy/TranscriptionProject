@echo off
setlocal
set "APPDATA=%~dp0.appdata"
if not exist "%APPDATA%" mkdir "%APPDATA%"
pushd "%~dp0"
dotnet run --project src\TranscriptMvp.csproj -- --demo %*
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%
