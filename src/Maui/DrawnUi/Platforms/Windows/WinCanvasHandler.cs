using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml;
using SkiaSharp.Views.Windows;
using SKPaintSurfaceEventArgs = SkiaSharp.Views.Maui.SKPaintSurfaceEventArgs;

namespace DrawnUi.Controls;

public partial class WinCanvasHandler : ViewHandler<ISKCanvasView, SKXamlCanvas>
{
    #region SHARED

    public static PropertyMapper<ISKCanvasView, WinCanvasHandler> SKCanvasViewMapper =
        new PropertyMapper<ISKCanvasView, WinCanvasHandler>(ViewHandler.ViewMapper)
        {
            [nameof(ISKCanvasView.EnableTouchEvents)] = MapEnableTouchEvents,
            [nameof(ISKCanvasView.IgnorePixelScaling)] = MapIgnorePixelScaling,
        };

    public static CommandMapper<ISKCanvasView, WinCanvasHandler> SKCanvasViewCommandMapper =
        new CommandMapper<ISKCanvasView, WinCanvasHandler>(ViewHandler.ViewCommandMapper)
        {
            [nameof(ISKCanvasView.InvalidateSurface)] = OnInvalidateSurface,
        };

    public WinCanvasHandler()
        : base(SKCanvasViewMapper, SKCanvasViewCommandMapper)
    {
    }

    public WinCanvasHandler(PropertyMapper? mapper, CommandMapper? commands)
        : base(mapper ?? SKCanvasViewMapper, commands ?? SKCanvasViewCommandMapper)
    {
    }

    #endregion

    private SKSizeI lastCanvasSize;

    protected override SKXamlCanvas CreatePlatformView() => new SKXamlCanvas();

    protected override void ConnectHandler(SKXamlCanvas platformView)
    {
        platformView.PaintSurface += OnPaintSurface;

        base.ConnectHandler(platformView);
    }

    protected override void DisconnectHandler(SKXamlCanvas platformView)
    {
        platformView.PaintSurface -= OnPaintSurface;

        base.DisconnectHandler(platformView);
    }

    // Mapper actions / properties
    public static void OnInvalidateSurface(WinCanvasHandler handler, ISKCanvasView canvasView, object? args)
    {
        if (handler?.PlatformView == null)
            return;

        try
        {
            handler.PlatformView.Invalidate();
        }
        catch (Exception e)
        {
            Super.Log(e);
        }
    }

    public static void MapIgnorePixelScaling(WinCanvasHandler handler, ISKCanvasView canvasView)
    {
        if (handler?.PlatformView == null)
            return;

        handler.PlatformView.IgnorePixelScaling = canvasView.IgnorePixelScaling;
    }

    public static void MapEnableTouchEvents(WinCanvasHandler handler, ISKCanvasView canvasView)
    {
        if (handler?.PlatformView == null)
            return;

    }

    // helper methods

    private void OnPaintSurface(object? sender, SkiaSharp.Views.Windows.SKPaintSurfaceEventArgs e)
    {
        var newCanvasSize = e.Info.Size;
        if (lastCanvasSize != newCanvasSize)
        {
            lastCanvasSize = newCanvasSize;
            VirtualView?.OnCanvasSizeChanged(newCanvasSize);
            //if (PlatformView is FrameworkElement element)
            //{
            //    element.UpdateLayout();
            //    element.Measure(new (element.ActualWidth, element.ActualHeight));
            //    element.Arrange(new (0, 0, element.ActualWidth, element.ActualHeight));
            //    element.UpdateLayout();
            //}
        }

        VirtualView?.OnPaintSurface(new SKPaintSurfaceEventArgs(e.Surface, e.Info, e.RawInfo));


    }

    private SKPoint OnGetScaledCoord(double x, double y)
    {
        if (VirtualView?.IgnorePixelScaling == false && PlatformView != null)
        {
            var scale = PlatformView.Dpi;

            x *= scale;
            y *= scale;
        }

        return new SKPoint((float)x, (float)y);
    }
}
