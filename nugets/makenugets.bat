@echo off
 

dotnet pack ..\src\Maui\DrawnUi\DrawnUi.Maui.csproj
dotnet pack ..\src\Maui\MetaPackage\AppoMobi.Maui.DrawnUi\AppoMobi.Maui.DrawnUi.csproj
dotnet pack ..\src\Maui\Addons\DrawnUi.Maui.Camera\DrawnUi.Maui.Camera.csproj
dotnet pack ..\src\Maui\Addons\DrawnUi.Maui.Game\DrawnUi.Maui.Game.csproj
dotnet pack ..\src\Maui\Addons\DrawnUi.Maui.MapsUi\DrawnUi.Maui.MapsUi.csproj
dotnet pack ..\src\Maui\Addons\DrawnUi.Maui.Rive\DrawnUi.Maui.Rive.csproj
dotnet pack ..\src\Maui\Addons\DrawnUi.Maui.Camera\DrawnUi.Maui.Camera.csproj
dotnet pack ..\src\Maui\Addons\DrawnUi.MauiGraphics\DrawnUi.MauiGraphics.csproj

pause
