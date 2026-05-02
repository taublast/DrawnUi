using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

namespace DrawnUi.Draw
{
    /// <summary>
    /// Blazor-only completion of <see cref="SkiaFontManager"/>.
    /// Adds HttpClient-based async font preloading. SharedNet base
    /// declares the partial with synchronous Initialize().
    /// </summary>
    public sealed partial class SkiaFontManager
    {
        public async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        {
            if (_fontSources.Count == 0)
            {
                Initialized = true;
                return;
            }

            await _loadSemaphore.WaitAsync(cancellationToken);
            try
            {
                var httpClient = services?.GetService(typeof(HttpClient)) as HttpClient;
                if (httpClient == null)
                {
                    Super.Log("[DRAWNUI] Blazor font preload skipped: HttpClient service was not found.", Microsoft.Extensions.Logging.LogLevel.Warning);
                    return;
                }

                foreach (var source in _fontSources)
                {
                    if (_fonts.ContainsKey(source.Key))
                    {
                        continue;
                    }

                    try
                    {
                        var bytes = await httpClient.GetByteArrayAsync(source.Value, cancellationToken);
                        using var data = SKData.CreateCopy(bytes);
                        var typeface = SKTypeface.FromData(data);
                        if (typeface != null)
                        {
                            _fonts[source.Key] = typeface;
                        }
                        else
                        {
                            Super.Log($"[DRAWNUI] Blazor font preload failed for {source.Key} from {source.Value}", Microsoft.Extensions.Logging.LogLevel.Warning);
                        }
                    }
                    catch (Exception e)
                    {
                        Super.Log(e);
                    }
                }

                Initialized = true;
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }
    }

    public static partial class DrawnExtensions
    {
        /// <summary>
        /// Blazor WebAssembly host bootstrap: preload registered fonts/images
        /// and run <see cref="Super.Init"/> before app startup.
        /// </summary>
        public static async Task<WebAssemblyHost> UseDrawnUiAsync(this WebAssemblyHostBuilder builder,
            DrawnUiStartupSettings settings = null,
            CancellationToken cancellationToken = default)
        {
            StartupSettings = settings;

            var host = builder.Build();

            Super.Services = host.Services;

            await SkiaFontManager.Instance.InitializeAsync(host.Services, cancellationToken);
            await SkiaImageManager.Instance.InitializeAsync(host.Services, cancellationToken);

            Super.Init();

            if (settings?.UseDesktopKeyboard == true)
            {
                var jsRuntime = host.Services.GetService<IJSRuntime>();
                if (jsRuntime != null)
                {
                    await KeyboardManager.AttachToKeyboardAsync(jsRuntime);
                }
            }

            return host;
        }
    }

    /// <summary>
    /// Blazor-only host wrapper used by JS interop.
    /// </summary>
    public static class BlazorAppHost
    {
    }
}

namespace DrawnUi.Blazor.Views
{
    public sealed class App
    {
        public static App Current { get; } = new();

        public Task CallJSAsync(string identifier, object arg1, bool arg2)
        {
            return Task.CompletedTask;
        }
    }
}
