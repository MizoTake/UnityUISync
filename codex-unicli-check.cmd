@echo off
setlocal
if /i "%~1"=="play" goto play
if /i "%~1"=="all" goto all
unicli check
if errorlevel 1 exit /b %errorlevel%
unicli exec Compile --json
if errorlevel 1 exit /b %errorlevel%
unicli exec TestRunner.RunEditMode "{\"resultFilter\":\"none\"}" --json
exit /b %errorlevel%

:play
unicli check
if errorlevel 1 exit /b %errorlevel%
unicli exec Compile --json
if errorlevel 1 exit /b %errorlevel%
unicli exec TestRunner.RunPlayMode "{\"resultFilter\":\"none\"}" --json
exit /b %errorlevel%

:all
call "%~f0"
if errorlevel 1 exit /b %errorlevel%
call "%~f0" play
exit /b %errorlevel%
