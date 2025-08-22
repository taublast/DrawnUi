using DrawnUi.Infrastructure;
using System.Diagnostics;

namespace PreviewTests.Views.Controls;

/// <summary>
/// Warning with CPU-rendering edges will not be blurred: https://issues.skia.org/issues/40036320
/// </summary>
public class TestShader : ContentLayout
{

    public TestShader()
    {
        CreateShader();
    }


    void CreateShader()
    {
        string shaderCode = SkSl.LoadFromResources($"{MauiProgram.ShadersFolder}/blur.sksl");
        _compiledShader = SkSl.Compile(shaderCode);
    }

    /// <summary>
    /// Reusing this
    /// </summary>
    protected SKPaint ImagePaint;

    /// <summary>
    /// Reusing this
    /// </summary>
    protected SKShader PaintShader;

    protected bool NeedInvalidateImageFilter { get; set; }

    public virtual void InvalidateImageFilter()
    {
        NeedInvalidateImageFilter = true;
    }

    private static void NeedChangeImageFilter(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaBackdrop control)
        {
            control.InvalidateImageFilter();
            NeedDraw(bindable, oldvalue, newvalue);
        }
    }

    public override void OnDisposing()
    {
        base.OnDisposing();

        PaintShader?.Dispose();
        PaintShader = null;

        ImagePaint?.Dispose();
        ImagePaint = null;

        _uniforms = null;

        _compiledShader?.Dispose();
        _compiledShader = null;
    }

    protected override void Paint(DrawingContext ctx)
    {
        if (IsDisposed)
        {
            //this will save a lot of trouble debugging unknown native crashes
            var message = $"[SkiaControl] Attempting to Paint a disposed control: {this}";
            Trace.WriteLine(message);
            throw new Exception(message);
        }


        if (ImagePaint == null)
        {
            ImagePaint = new()
            {
            };
        }


        if (NeedInvalidateImageFilter)
        {
            NeedInvalidateImageFilter = false;
            // PaintImageFilter?.Dispose(); //might be used in double buffered!
            PaintShader = null;
        }

        var destination = ctx.Destination;

        if (destination.Width > 0 && destination.Height > 0 && _compiledShader != null)
        {
            DrawViews(ctx);

            //notice we read from the real canvas and we write to ctx.Canvas which can be cache
            ctx.Context.Superview.CanvasView.Surface.Canvas.Flush();
            var snapshot = ctx.Context.Superview.CanvasView.Surface.Snapshot(new((int)destination.Left, (int)destination.Top, (int)destination.Right, (int)destination.Bottom));

            BuildTextures(snapshot);

            //if (PaintShader == null)
            {
                _uniforms = BuildUniforms(snapshot);
            }

            PaintShader = _compiledShader.ToShader(_uniforms, _passTextures);

            ImagePaint.Shader = PaintShader;

            {
                if (snapshot != null)
                {
                    PaintTintBackground(ctx.Context.Canvas, ctx.Destination);

                    ctx.Context.Canvas.DrawRect(ctx.Destination, ImagePaint);
                }

            }

        }

    }

    SKRuntimeEffectUniforms BuildUniforms(SKImage texture)
    {
        var viewport = DrawingRect;

        //float margin = (float)Math.Round(VerticalMargin * RenderingScale);
        //float timeValue = _mouse.Y;//0.0f;
        SKSize iResolution = new(viewport.Width, viewport.Height);
        SKSize iImageResolution = new(texture.Width, texture.Height);

        var uniforms = new SKRuntimeEffectUniforms(_compiledShader);

        uniforms["iOffset"] = new[] { viewport.Left, viewport.Top };
        //uniforms["iOrigin"] = new[] { _origin.X, _origin.Y };
        //uniforms["iMargins"] = new[] { 0, margin, 0, margin };

        uniforms["iResolution"] = new[] { iResolution.Width, iResolution.Height };
        uniforms["iImageResolution"] = new[] { iImageResolution.Width, iImageResolution.Height };
        //uniforms["iTime"] = timeValue;
        //uniforms["iMouse"] = new[] { _mouse.X, _mouse.Y, 0f, 0f };

        return uniforms;
    }

    /// <summary>
    /// Compiled script
    /// </summary>
    SKRuntimeEffect _compiledShader;

    SKShader _textureFront;

    /// <summary>
    /// _texture wrapper for later use
    /// </summary>
    private SKRuntimeEffectChildren _passTextures;

    private SKRuntimeEffectUniforms _uniforms;

    void BuildTextures(SKImage front)
    {
        try
        {
            var disposeTexture1 = _textureFront;

            if (_compiledShader != null && front != null)
            {
                _textureFront = front.ToShader();
            }

            if (_textureFront != null)
            {
                {
                    _passTextures = new SKRuntimeEffectChildren(_compiledShader)
                    {
                        { "iImage1", _textureFront },
                    };
                }
            }

            disposeTexture1?.Dispose();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

}
