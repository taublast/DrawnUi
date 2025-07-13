@echo off
setlocal enabledelayedexpansion

REM Get the directory where the batch file is located
set "batch_dir=%~dp0"

REM Ensure the directory does not end with a backslash
if "%batch_dir:~-1%"=="\" set "batch_dir=%batch_dir:~0,-1%"

REM Debugging: Print the batch directory
echo Batch directory: %batch_dir%

REM Define source directories relative to the batch file location
set "source_dir1=%batch_dir%\..\src\Maui\DrawnUi\bin\Release"
set "source_dir2=%batch_dir%\..\src\Maui\MetaPackage\AppoMobi.Maui.DrawnUi\bin\Release"
set "source_dir3=%batch_dir%\..\src\Maui\Addons\DrawnUi.Maui.Camera\bin\Release"
set "source_dir4=%batch_dir%\..\src\Maui\Addons\DrawnUi.Maui.Game\bin\Release"
set "source_dir5=%batch_dir%\..\src\Maui\Addons\DrawnUi.Maui.MapsUi\bin\Release"
set "source_dir6=%batch_dir%\..\src\Maui\Addons\DrawnUi.Maui.Rive\bin\Release"
set "source_dir7=%batch_dir%\..\src\Maui\Addons\DrawnUi.MauiGraphics\bin\Release"
set "destination_dir=C:\Nugets"

REM Define file masks
set "file_mask1=*.nupkg"
set "file_mask2=*.snupkg"

REM Define array of source directories
set source_dirs=source_dir1 source_dir2 source_dir3 source_dir4 source_dir5 source_dir6 source_dir7

REM Define array of file masks
set file_masks=file_mask1 file_mask2

REM Loop through source directories and file masks
for %%d in (%source_dirs%) do (
    for %%m in (%file_masks%) do (
        set "current_source=!%%d!"
        set "current_mask=!%%m!"
        call :MoveFiles "!current_source!" "!current_mask!"
    )
)

REM Open the Nugets folder
explorer "%destination_dir%"

cd %destination_dir%

pause

goto :eof

REM Function to move files
:MoveFiles
    setlocal
    set "source_dir=%~1"
    set "file_mask=%~2"

    REM Debugging: Print the raw parameters received
    echo Raw source_dir: %~1
    echo Raw file_mask: %~2

    REM Debugging: Print the processed source directory and file mask
    echo Debug: source_dir=%source_dir%
    echo Debug: file_mask=%file_mask%

    REM Resolve the absolute path
    pushd "%source_dir%" >nul 2>&1
    if errorlevel 1 (
        echo Directory "%source_dir%" does not exist or is inaccessible.
        endlocal
        goto :eof
    )
    set "abs_source_dir=%cd%"
    popd >nul

    echo Moving from "%abs_source_dir%\%file_mask%" to "%destination_dir%"

    move "%abs_source_dir%\%file_mask%" "%destination_dir%\"
    if errorlevel 1 (
        echo An error occurred while moving the files from "%abs_source_dir%".
    ) else (
        echo Files moved successfully from "%abs_source_dir%".
    )
    endlocal
    goto :eof
