@echo off
setlocal enabledelayedexpansion

REM Check if API key is provided as a parameter
if "%~1"=="" (
    REM Ask for API key
    set /p "APIKEY=Please enter your GitHub API key: "
) else (
    set "APIKEY=%~1"
)

REM Define the source directory for the packages
set "source_dir=C:\Nugets"

REM Define the list of file masks for the packages
set "mask[1]=DrawnUi.Maui*.1.6.1.1*.nupkg"
set "mask[2]=AppoMobi.Maui.DrawnUi.1.6.1.1*.*nupkg"
set "mask_count=2"

REM Loop through each file mask
for /L %%i in (1,1,%mask_count%) do (
    set "file_mask=!mask[%%i]!"
    echo Using file mask "!file_mask!"
    REM Loop through each package file matching the mask in the source directory
    for %%f in ("!source_dir!\!file_mask!") do (
        echo Uploading "%%f" to GitHub with API key.
        dotnet nuget push --source github "%%f" --api-key %APIKEY%
        if errorlevel 1 (
            echo An error occurred while uploading "%%f".
        ) else (
            echo "%%f" uploaded successfully.
        )
    )
)

pause
endlocal