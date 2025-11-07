@echo off
 

dotnet pack ..\src\Maui\DrawnUi\DrawnUi.Maui.csproj -c Release
dotnet pack ..\src\Maui\MetaPackage\AppoMobi.Maui.DrawnUi\AppoMobi.Maui.DrawnUi.csproj -c Release
dotnet pack ..\src\Maui\Addons\DrawnUi.Maui.Camera\DrawnUi.Maui.Camera.csproj -c Release
dotnet pack ..\src\Maui\Addons\DrawnUi.Maui.Game\DrawnUi.Maui.Game.csproj -c Release
dotnet pack ..\src\Maui\Addons\DrawnUi.Maui.MapsUi\DrawnUi.Maui.MapsUi.csproj -c Release
dotnet pack ..\src\Maui\Addons\DrawnUi.Maui.Rive\DrawnUi.Maui.Rive.csproj -c Release
dotnet pack ..\src\Maui\Addons\DrawnUi.Maui.Camera\DrawnUi.Maui.Camera.csproj -c Release
dotnet pack ..\src\Maui\Addons\DrawnUi.MauiGraphics\DrawnUi.MauiGraphics.csproj -c Release

pause
