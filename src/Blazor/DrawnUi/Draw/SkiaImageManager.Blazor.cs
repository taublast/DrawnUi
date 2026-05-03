using System.Collections.Concurrent;
using DrawnUi.Infrastructure.Models;
using DrawnUi.Infrastructure.Xaml;
using System.IO;

namespace DrawnUi.Draw;

public class SkiaImageManager : IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<SKBitmap>> _inFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _registeredSources = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _initializeSemaphore = new(1, 1);

    /// <summary>
    /// Limits concurrent HTTP image loads to avoid memory/CPU pressure from many simultaneous
    /// decode operations. Browser fetch is non-blocking so without this all requests fire at once.
    /// </summary>
    private static readonly SemaphoreSlim _loadSemaphore = new(8, 8);

    private sealed record CacheEntry(SKBitmap Bitmap, DateTimeOffset ExpiresAtUtc);

    public static bool LoadLocalAsync = true;
    public static bool ReuseBitmaps = false;
    public static int CacheLongevitySecs = 1800;
    public static string NativeFilePrefix = "file://";
    public static bool LogEnabled = false;
    public static bool UseCache { get; set; } = true;
    public static bool EnableHttpRetry = true;
    public static int HttpRetryMaxAttempts = 3;

    private static SkiaImageManager _instance;

    public static SkiaImageManager Instance => _instance ??= new SkiaImageManager();

    public bool IsDisposed { get; protected set; }
    public bool IsLoadingLocked { get; set; }
    public bool IsOffline { get; protected set; }

    public event EventHandler CanReload;

    public bool Initialized { get; private set; }

    public static void TraceLog(string message)
    {
        if (LogEnabled)
        {
            Console.WriteLine(message);
        }
    }

    public void CancelAll()
    {
    }

    public void RegisterImage(string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            throw new ArgumentException("Image source URL cannot be empty.", nameof(sourceUrl));
        }

        var normalized = NormalizeFilePath(sourceUrl);
        _registeredSources[normalized] = normalized;
    }

    public void RegisterImage(string alias, string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new ArgumentException("Image alias cannot be empty.", nameof(alias));
        }

        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            throw new ArgumentException("Image source URL cannot be empty.", nameof(sourceUrl));
        }

        _registeredSources[NormalizeFilePath(alias)] = NormalizeFilePath(sourceUrl);
    }

    public async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (_registeredSources.Count == 0)
        {
            Initialized = true;
            return;
        }

        await _initializeSemaphore.WaitAsync(cancellationToken);
        try
        {
            var httpClient = services?.GetService<HttpClient>();
            if (httpClient == null)
            {
                Super.Log("[DRAWNUI] Blazor image preload skipped: HttpClient service was not found.", Microsoft.Extensions.Logging.LogLevel.Warning);
                return;
            }

            foreach (var registration in _registeredSources)
            {
                if (GetFromCacheInternal(registration.Key) != null)
                {
                    continue;
                }

                try
                {
                    var bytes = await httpClient.GetByteArrayAsync(registration.Value, cancellationToken);
                    var bitmap = SKBitmap.Decode(bytes);
                    if (bitmap == null)
                    {
                        Super.Log($"[DRAWNUI] Blazor image preload failed for {registration.Key} from {registration.Value}", Microsoft.Extensions.Logging.LogLevel.Warning);
                        continue;
                    }

                    AddToCache(registration.Key, bitmap, CacheLongevitySecs);
                    if (!string.Equals(registration.Key, registration.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        AddToCache(registration.Value, bitmap, CacheLongevitySecs);
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
            _initializeSemaphore.Release();
        }
    }

    public virtual Task<SKBitmap> LoadImageAsync(ImageSource source, CancellationToken token)
    {
        return LoadImageOnPlatformAsync(source, token);
    }

    public virtual Task<SKBitmap> LoadImageManagedAsync(ImageSource source, CancellationTokenSource token, LoadPriority priority = LoadPriority.Normal)
    {
        if (source == null || source.IsEmpty)
        {
            return Task.FromResult<SKBitmap>(null);
        }

        if (IsLoadingLocked)
        {
            return Task.FromCanceled<SKBitmap>(token.Token);
        }

        var cacheKey = GetUriFromImageSource(source);
        if (UseCache && !string.IsNullOrEmpty(cacheKey))
        {
            var cached = GetFromCache(cacheKey);
            if (cached != null)
            {
                TraceLog($"[BlazorSkiaImageManager] cache hit {cacheKey}");
                return Task.FromResult(cached);
            }

            var task = _inFlight.GetOrAdd(cacheKey, _ => LoadAndCacheAsync(source, cacheKey, token.Token));
            return AwaitTrackedLoadAsync(cacheKey, task, token.Token);
        }

        return LoadImageOnPlatformAsync(source, token.Token);
    }

    private async Task<SKBitmap> AwaitTrackedLoadAsync(string cacheKey, Task<SKBitmap> task, CancellationToken cancellationToken)
    {
        try
        {
            var bitmap = await task.WaitAsync(cancellationToken);
            if (bitmap == null)
            {
                return null;
            }

            return ReuseBitmaps ? bitmap : bitmap.Copy();
        }
        finally
        {
            _inFlight.TryRemove(cacheKey, out _);
        }
    }

    private async Task<SKBitmap> LoadAndCacheAsync(ImageSource source, string cacheKey, CancellationToken cancellationToken)
    {
        var bitmap = await LoadImageOnPlatformAsync(source, cancellationToken);
        if (bitmap == null)
        {
            return null;
        }

        AddToCache(cacheKey, bitmap, CacheLongevitySecs);
        return bitmap;
    }

    public async Task Preload(ImageSource source, CancellationTokenSource cts)
    {
        if (source == null || source.IsEmpty)
        {
            return;
        }

        await LoadImageManagedAsync(source, cts);
    }

    public virtual async Task PreloadImage(ImageSource source, CancellationTokenSource cancel = default)
    {
        using var localCancel = cancel == null ? new CancellationTokenSource() : null;
        await Preload(source, cancel ?? localCancel);
    }

    public virtual async Task PreloadImage(string source, CancellationTokenSource cancel = default)
    {
        using var localCancel = cancel == null ? new CancellationTokenSource() : null;
        if (!string.IsNullOrWhiteSpace(source))
        {
            await Preload(FrameworkImageSourceConverter.FromInvariantString(source), cancel ?? localCancel);
        }
    }

    public virtual async Task PreloadImages(IList<string> list, CancellationTokenSource cancel = default)
    {
        if (list == null || list.Count == 0)
        {
            return;
        }

        using var localCancel = cancel == null ? new CancellationTokenSource() : null;
        var cts = cancel ?? localCancel;
        var tasks = list
            .TakeWhile(_ => !cts.IsCancellationRequested)
            .Select(source => PreloadImage(source, cts))
            .ToList();
        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    public virtual async Task PreloadBanners<T>(IList<T> list, CancellationTokenSource cancel = default) where T : IHasBanner
    {
        if (list == null || list.Count == 0)
        {
            return;
        }

        using var localCancel = cancel == null ? new CancellationTokenSource() : null;
        var cts = cancel ?? localCancel;
        var tasks = list
            .TakeWhile(_ => !cts.IsCancellationRequested)
            .Select(item =>
            {
                item.BannerPreloadOrdered = true;
                return PreloadImage(item.Banner, cts);
            })
            .ToList();
        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    public void UpdateInCache(string uri, SKBitmap bitmap, int cacheLongevityMinutes)
    {
        if (string.IsNullOrWhiteSpace(uri) || bitmap == null)
        {
            return;
        }

        _cache[uri] = new CacheEntry(bitmap, DateTimeOffset.UtcNow.AddMinutes(cacheLongevityMinutes));
    }

    public bool AddToCache(string uri, SKBitmap bitmap, int cacheLongevitySecs)
    {
        if (!UseCache || string.IsNullOrWhiteSpace(uri) || bitmap == null)
        {
            return false;
        }

        CleanupExpired(uri);
        if (_cache.ContainsKey(uri))
        {
            return false;
        }

        _cache[uri] = new CacheEntry(bitmap, DateTimeOffset.UtcNow.AddSeconds(cacheLongevitySecs));
        return true;
    }

    public SKBitmap GetFromCache(string url)
    {
        var bitmap = GetFromCacheInternal(url);
        if (bitmap == null)
        {
            return null;
        }

        return ReuseBitmaps ? bitmap : bitmap.Copy();
    }

    public SKBitmap GetFromCacheInternal(string url)
    {
        if (!UseCache || string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        CleanupExpired(url);
        return _cache.TryGetValue(url, out var cached) ? cached.Bitmap : null;
    }

    private void CleanupExpired(string url)
    {
        if (_cache.TryGetValue(url, out var cached) && cached.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            _cache.TryRemove(url, out _);
        }
    }

    /// <summary>
    /// Opens a fully-buffered stream for any source (local asset or URL).
    /// On Blazor/WASM the HTTP concurrency limit is respected and the result is
    /// always a seekable <see cref="MemoryStream"/> so callers can use it immediately
    /// after the returned task completes without holding a connection open.
    /// </summary>
    public static async Task<Stream> OpenStreamAsync(string source, CancellationToken cancel = default)
    {
        if (source.SafeContainsInLower(NativeFilePrefix))
        {
            var fullFilename = source.Replace(NativeFilePrefix, "");
            return new FileStream(fullFilename, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        var httpClient = Super.Services?.GetService<HttpClient>()
            ?? throw new InvalidOperationException("[SkiaImageManager] HttpClient service was not found.");

        await _loadSemaphore.WaitAsync(cancel);
        try
        {
            using var httpStream = await httpClient.GetStreamAsync(source, cancel);
            var ms = new MemoryStream();
            await httpStream.CopyToAsync(ms, cancel);
            ms.Position = 0;
            return ms;
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }

    public static string GetUriFromImageSource(ImageSource source)
    {
        if (source is UriImageSource uriSource)
        {
            return uriSource.Uri?.ToString();
        }

        if (source is FileImageSource fileSource)
        {
            return NormalizeFilePath(fileSource.File);
        }

        if (source is ImageSourceResourceStream resourceStream)
        {
            return resourceStream.Url;
        }

        if (source is StreamImageSource)
        {
            return Guid.NewGuid().ToString();
        }

        return null;
    }

    public void Dispose()
    {
        IsDisposed = true;
        _inFlight.Clear();
        _cache.Clear();
    }

    public static async Task<SKBitmap> LoadFromFile(string filename, CancellationToken cancel)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return null;
        }

        if (filename.SafeContainsInLower(NativeFilePrefix))
        {
            TraceLog($"[BlazorSkiaImageManager] Native file loading is not supported in browser for {filename}");
            return null;
        }

        var cacheKey = NormalizeFilePath(filename);
        var sourcePath = Instance.ResolveRegisteredSource(cacheKey);
        var cached = Instance.GetFromCacheInternal(cacheKey);
        if (cached != null)
        {
            return ReuseBitmaps ? cached : cached.Copy();
        }

        try
        {
            var client = GetHttpClient();
            if (client == null)
            {
                return null;
            }

            await _loadSemaphore.WaitAsync(cancel);
            try
            {
                var bytes = await client.GetByteArrayAsync(sourcePath, cancel);
                var bitmap = DecodeBitmap(bytes);
                if (bitmap != null)
                {
                    Instance.AddToCache(cacheKey, bitmap, CacheLongevitySecs);
                    if (!string.Equals(cacheKey, sourcePath, StringComparison.OrdinalIgnoreCase))
                    {
                        Instance.AddToCache(sourcePath, bitmap, CacheLongevitySecs);
                    }
                    return ReuseBitmaps ? bitmap : bitmap.Copy();
                }
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Super.Log(e);
        }

        return null;
    }

    public static async Task<SKBitmap> LoadImageFromInternetAsync(UriImageSource uriSource, CancellationToken cancel)
    {
        if (uriSource?.Uri == null)
        {
            return null;
        }

        if (!EnableHttpRetry)
        {
            return await LoadImageFromInternetAsyncSingle(uriSource.Uri, cancel);
        }

        for (var attempt = 0; attempt <= HttpRetryMaxAttempts; attempt++)
        {
            try
            {
                var bitmap = await LoadImageFromInternetAsyncSingle(uriSource.Uri, cancel);
                if (bitmap != null)
                {
                    return bitmap;
                }

                if (attempt < HttpRetryMaxAttempts)
                {
                    await Task.Delay((int)Math.Pow(2, attempt) * 100, cancel);
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception e) when (attempt < HttpRetryMaxAttempts)
            {
                TraceLog($"[BlazorSkiaImageManager] retry {attempt + 1} for {uriSource.Uri}: {e.Message}");
                await Task.Delay((int)Math.Pow(2, attempt) * 100, cancel);
            }
        }

        return null;
    }

    private static async Task<SKBitmap> LoadImageFromInternetAsyncSingle(Uri uri, CancellationToken cancel)
    {
        try
        {
            var client = GetHttpClient();
            if (client == null)
            {
                return null;
            }

            await _loadSemaphore.WaitAsync(cancel);
            try
            {
                var bytes = await client.GetByteArrayAsync(uri, cancel);
                return DecodeBitmap(bytes);
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Super.Log(e);
        }

        return null;
    }

    public static async Task<SKBitmap> LoadImageOnPlatformAsync(ImageSource source, CancellationToken cancel)
    {
        if (source == null)
        {
            return null;
        }

        try
        {
            if (source is StreamImageSource streamSource)
            {
                using var stream = await streamSource.Stream(cancel);
                return await DecodeBitmapAsync(stream, cancel);
            }

            if (source is UriImageSource uriSource)
            {
                return await LoadImageFromInternetAsync(uriSource, cancel);
            }

            if (source is FileImageSource fileSource)
            {
                return await LoadFromFile(fileSource.File, cancel);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Super.Log(e);
        }

        return null;
    }

    private static HttpClient GetHttpClient()
    {
        return Super.Services?.GetService<HttpClient>();
    }

    private static SKBitmap DecodeBitmap(byte[] bytes)
    {
        return bytes == null || bytes.Length == 0 ? null : SKBitmap.Decode(bytes);
    }

    private static async Task<SKBitmap> DecodeBitmapAsync(Stream stream, CancellationToken cancel)
    {
        if (stream == null)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancel);
        return DecodeBitmap(buffer.ToArray());
    }

    private string ResolveRegisteredSource(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return _registeredSources.TryGetValue(path, out var registered) ? registered : path;
    }

    private static string NormalizeFilePath(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return string.Empty;
        }

        var normalized = filename.Replace('\\', '/');
        if (normalized.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[7..];
        }

        normalized = normalized.TrimStart('.');
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized.TrimStart('/');
        }

        return normalized;
    }
}