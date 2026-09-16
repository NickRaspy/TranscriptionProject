@echo off
setlocal
set "APPDATA=%~dp0.appdata"
if not exist "%APPDATA%" mkdir "%APPDATA%"
pushd "%~dp0"
if exist "tests\obj\project.assets.json" (
    dotnet build TranscriptMvp.sln --no-restore %*
) else (
    dotnet build TranscriptMvp.sln %*
)
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%
