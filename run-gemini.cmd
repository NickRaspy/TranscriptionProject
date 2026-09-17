@echo off
setlocal DisableDelayedExpansion
set "APPDATA=%~dp0.appdata"
if not exist "%APPDATA%" mkdir "%APPDATA%"
if exist "%~dp0.env" (
    for /f "usebackq eol=# tokens=1,* delims==" %%A in ("%~dp0.env") do (
        if /i "%%A"=="GEMINI_API_KEY" set "GEMINI_API_KEY=%%B"
    )
)
if not defined GEMINI_API_KEY (
    echo GEMINI_API_KEY is not set. Add it to the .env file, then run this script again.
    exit /b 1
)
pushd "%~dp0"
dotnet run --project src\TranscriptMvp.csproj -- --gemini --output gemini-output %*
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%
