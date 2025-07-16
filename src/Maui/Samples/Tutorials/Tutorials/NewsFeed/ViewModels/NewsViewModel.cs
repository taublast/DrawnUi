using System.Diagnostics;
using System.Windows.Input;
using AppoMobi.Specials;
using DrawnUI.Tutorials.NewsFeed.Models;
using DrawnUI.Tutorials.NewsFeed.Services;

namespace DrawnUI.Tutorials.NewsFeed.ViewModels;

public class NewsViewModel : BaseViewModel
{
    private readonly NewsDataProvider _dataProvider;
    private CancellationTokenSource _preloadCancellation;
    
    public NewsViewModel()
    {
        _dataProvider = new NewsDataProvider();
        NewsItems = new ObservableRangeCollection<NewsItem>();
        
        RefreshCommand = new Command(async () => await RefreshFeed());
        LoadMoreCommand = new Command(async () => await LoadMore());
        
        // Load initial data
        _ = RefreshFeed();
    }
    
    public ObservableRangeCollection<NewsItem> NewsItems { get; }
    
    public ICommand RefreshCommand { get; }
    public ICommand LoadMoreCommand { get; }

    private int DataChunkSize = 100;
    
    private async Task RefreshFeed()
    {
        if (IsBusy) return;
        
        IsBusy = true;
        
        try
        {
            // Cancel previous preloading
            _preloadCancellation?.Cancel();

            Debug.WriteLine($"Loading news feed !!!");

            // Generate fresh content
            var newItems = _dataProvider.GetNewsFeed(DataChunkSize);
            
            // Preload images in background (DrawnUI's SkiaImageManager)
            _preloadCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            _ = PreloadImages(newItems, _preloadCancellation.Token);
            
            // Update UI - Replace all items for refresh
            MainThread.BeginInvokeOnMainThread(() =>
            {
                NewsItems.ReplaceRange(newItems);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error refreshing feed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task LoadMore()
    {
        if (IsBusy) return;
        
        IsBusy = true;
        
        try
        {
            Debug.WriteLine("Loading more items !!!");
            var newItems = _dataProvider.GetNewsFeed(DataChunkSize/2);
            
            // Preload new images
            _preloadCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            _ = PreloadImages(newItems, _preloadCancellation.Token);
            
            // Add new items to the end of the collection
            MainThread.BeginInvokeOnMainThread(() =>
            {
                NewsItems.AddRange(newItems);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading more: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task PreloadImages(List<NewsItem> items, CancellationToken cancellationToken)
    {
        try
        {
            var imageUrls = new List<string>();

            // Add content images
            imageUrls.AddRange(items
                .Where(x => !string.IsNullOrEmpty(x.ImageUrl))
                .Select(x => x.ImageUrl));

            // Add avatar images
            imageUrls.AddRange(items
                .Where(x => !string.IsNullOrEmpty(x.AuthorAvatarUrl))
                .Select(x => x.AuthorAvatarUrl));

            // Use DrawnUI's image manager for efficient preloading
            await SkiaImageManager.Instance.PreloadImages(imageUrls, _preloadCancellation);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error preloading images: {ex.Message}");
        }
    }
}
