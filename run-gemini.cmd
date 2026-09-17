@echo off
setlocal DisableDelayedExpansion
set "APPDATA=%~dp0.appdata"
if not exist "%APPDATA%" mkdir "%APPDATA%"
if not defined GEMINI_API_KEY (
    echo GEMINI_API_KEY is not set. Set it in the environment, then run this script again.
    exit /b 1
)
pushd "%~dp0"
dotnet run --project src\TranscriptMvp.csproj -- --gemini --output gemini-output %*
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%
