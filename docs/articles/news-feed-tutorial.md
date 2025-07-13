# News Feed Tutorial: One Cell to Rule Them All

Building a news feed with mixed content types (text posts, images, videos, articles, ads) is a common requirement. With DrawnUI, you get the freedom to **just draw what you need** using one smart cell that adapts to any content type.

<details>
<summary>📖 For developers familiar with MAUI DataTemplateSelector</summary>

Traditional MAUI approaches typically use DataTemplateSelector with multiple templates:

```csharp
public class NewsDataTemplateSelector : DataTemplateSelector
{
    public DataTemplate TextPostTemplate { get; set; }
    public DataTemplate ImagePostTemplate { get; set; }  
    public DataTemplate VideoPostTemplate { get; set; }
    public DataTemplate ArticleTemplate { get; set; }
    public DataTemplate AdTemplate { get; set; }
    
    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        return news.Type switch
        {
            NewsType.Text => TextPostTemplate,
            NewsType.Image => ImagePostTemplate,
            // ... etc
        };
    }
}
```

This involves multiple XAML templates, template selection logic, and different cell types that can't share recycling pools.

</details>

## The DrawnUI Way: One Universal Cell

With DrawnUI, we use one smart cell that simply shows or hides elements based on content type. All recycling and height calculation happens automatically:

### 1. Define Content Types

```csharp
public enum NewsType
{
    Text,
    Image, 
    Video,
    Article,
    Ad
}

public class NewsItem
{
    public NewsType Type { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public string ImageUrl { get; set; }
    public string VideoUrl { get; set; }
    public string ArticleUrl { get; set; }
    public string AuthorName { get; set; }
    public DateTime PublishedAt { get; set; }
    public int LikesCount { get; set; }
    public int CommentsCount { get; set; }
}
```

### 2. Create Universal News Cell

```xml
<draw:SkiaDynamicDrawnCell
    x:Class="YourApp.Views.NewsCell"
    BackgroundColor="White"
    Margin="0,4"
    Padding="16">
    
    <draw:SkiaLayout Type="Column" Spacing="12">
        
        <!-- Author Header -->
        <draw:SkiaLayout Type="Row" Spacing="8" HorizontalOptions="Fill">
            <draw:SkiaShape
                x:Name="AvatarFrame"
                Type="Circle"
                WidthRequest="40"
                HeightRequest="40"
                Fill="LightGray" />
                
            <draw:SkiaLayout Type="Column" HorizontalOptions="Fill">
                <draw:SkiaLabel
                    x:Name="AuthorLabel"
                    FontSize="14"
                    FontWeight="Bold"
                    TextColor="Black" />
                <draw:SkiaLabel
                    x:Name="TimeLabel"
                    FontSize="12"
                    TextColor="Gray" />
            </draw:SkiaLayout>
        </draw:SkiaLayout>
        
        <!-- Content Title -->
        <draw:SkiaLabel
            x:Name="TitleLabel"
            FontSize="16"
            FontWeight="Bold"
            TextColor="Black"
            IsVisible="False" />
            
        <!-- Text Content -->
        <draw:SkiaLabel
            x:Name="ContentLabel"
            FontSize="14"
            TextColor="#333"
            LineBreakMode="WordWrap"
            IsVisible="False" />
            
        <!-- Image Content -->
        <draw:SkiaImage
            x:Name="ContentImage"
            CornerRadius="8"
            Aspect="AspectFill"
            HeightRequest="200"
            IsVisible="False" />
            
        <!-- Video Thumbnail with Play Button -->
        <draw:SkiaLayout
            x:Name="VideoLayout"
            Type="Absolute"
            HeightRequest="200"
            IsVisible="False">
            
            <draw:SkiaImage
                x:Name="VideoThumbnail"
                CornerRadius="8"
                Aspect="AspectFill"
                HorizontalOptions="Fill"
                VerticalOptions="Fill" />
                
            <draw:SkiaShape
                Type="Circle"
                WidthRequest="60"
                HeightRequest="60"
                Fill="Black"
                Opacity="0.7"
                HorizontalOptions="Center"
                VerticalOptions="Center">
                <draw:SkiaShape.Shadow>
                    <draw:SkiaShadow Color="Black" BlurRadius="10" />
                </draw:SkiaShape.Shadow>
            </draw:SkiaShape>
            
            <draw:SkiaSvg
                Source="play_icon.svg"
                WidthRequest="24"
                HeightRequest="24"
                TintColor="White"
                HorizontalOptions="Center"
                VerticalOptions="Center" />
        </draw:SkiaLayout>
        
        <!-- Article Preview -->
        <draw:SkiaLayout
            x:Name="ArticleLayout"
            Type="Row"
            Spacing="12"
            IsVisible="False">
            
            <draw:SkiaImage
                x:Name="ArticleThumbnail"
                WidthRequest="80"
                HeightRequest="80"
                CornerRadius="4"
                Aspect="AspectFill" />
                
            <draw:SkiaLayout Type="Column" HorizontalOptions="Fill">
                <draw:SkiaLabel
                    x:Name="ArticleTitle"
                    FontSize="14"
                    FontWeight="Bold"
                    TextColor="Black"
                    LineBreakMode="TailTruncation"
                    MaxLines="2" />
                <draw:SkiaLabel
                    x:Name="ArticleDescription"
                    FontSize="12"
                    TextColor="Gray"
                    LineBreakMode="TailTruncation"
                    MaxLines="3" />
            </draw:SkiaLayout>
        </draw:SkiaLayout>
        
        <!-- Ad Content -->
        <draw:SkiaLayout
            x:Name="AdLayout"
            Type="Column"
            Spacing="8"
            IsVisible="False">
            
            <draw:SkiaLabel
                Text="Sponsored"
                FontSize="10"
                TextColor="Gray"
                HorizontalOptions="End" />
                
            <draw:SkiaImage
                x:Name="AdImage"
                CornerRadius="8"
                Aspect="AspectFill"
                HeightRequest="150" />
                
            <draw:SkiaLabel
                x:Name="AdTitle"
                FontSize="14"
                FontWeight="Bold"
                TextColor="Black" />
        </draw:SkiaLayout>
        
        <!-- Interaction Bar -->
        <draw:SkiaLayout Type="Row" Spacing="16" HorizontalOptions="Fill">
            <draw:SkiaButton
                x:Name="LikeButton"
                Text="👍"
                BackgroundColor="Transparent"
                TextColor="Gray"
                FontSize="14" />
                
            <draw:SkiaButton
                x:Name="CommentButton"
                Text="💬"
                BackgroundColor="Transparent"
                TextColor="Gray"
                FontSize="14" />
                
            <draw:SkiaButton
                x:Name="ShareButton"
                Text="📤"
                BackgroundColor="Transparent"
                TextColor="Gray"
                FontSize="14"
                HorizontalOptions="End" />
        </draw:SkiaLayout>
        
    </draw:SkiaLayout>
</draw:SkiaDynamicDrawnCell>
```

### 3. Smart Cell Logic - Content-Driven Behavior

```csharp
public partial class NewsCell : SkiaDynamicDrawnCell
{
    public NewsCell()
    {
        InitializeComponent();
    }

    protected override void OnItemSet()
    {
        base.OnItemSet();
        
        if (ItemData is NewsItem news)
        {
            ConfigureForContentType(news);
        }
    }

    private void ConfigureForContentType(NewsItem news)
    {
        // Reset all content visibility
        HideAllContent();
        
        // Configure common elements
        AuthorLabel.Text = news.AuthorName;
        TimeLabel.Text = GetRelativeTime(news.PublishedAt);
        LikeButton.Text = $"👍 {news.LikesCount}";
        CommentButton.Text = $"💬 {news.CommentsCount}";
        
        // Configure based on content type
        switch (news.Type)
        {
            case NewsType.Text:
                ConfigureTextPost(news);
                break;
                
            case NewsType.Image:
                ConfigureImagePost(news);
                break;
                
            case NewsType.Video:
                ConfigureVideoPost(news);
                break;
                
            case NewsType.Article:
                ConfigureArticlePost(news);
                break;
                
            case NewsType.Ad:
                ConfigureAdPost(news);
                break;
        }
    }

    private void HideAllContent()
    {
        TitleLabel.IsVisible = false;
        ContentLabel.IsVisible = false;
        ContentImage.IsVisible = false;
        VideoLayout.IsVisible = false;
        ArticleLayout.IsVisible = false;
        AdLayout.IsVisible = false;
    }

    private void ConfigureTextPost(NewsItem news)
    {
        if (!string.IsNullOrEmpty(news.Title))
        {
            TitleLabel.Text = news.Title;
            TitleLabel.IsVisible = true;
        }
        
        ContentLabel.Text = news.Content;
        ContentLabel.IsVisible = true;
    }

    private void ConfigureImagePost(NewsItem news)
    {
        ContentImage.Source = news.ImageUrl;
        ContentImage.IsVisible = true;
        
        if (!string.IsNullOrEmpty(news.Content))
        {
            ContentLabel.Text = news.Content;
            ContentLabel.IsVisible = true;
        }
    }

    private void ConfigureVideoPost(NewsItem news)
    {
        VideoThumbnail.Source = ExtractVideoThumbnail(news.VideoUrl);
        VideoLayout.IsVisible = true;
        
        if (!string.IsNullOrEmpty(news.Content))
        {
            ContentLabel.Text = news.Content;
            ContentLabel.IsVisible = true;
        }
    }

    private void ConfigureArticlePost(NewsItem news)
    {
        ArticleThumbnail.Source = news.ImageUrl;
        ArticleTitle.Text = news.Title;
        ArticleDescription.Text = news.Content;
        ArticleLayout.IsVisible = true;
    }

    private void ConfigureAdPost(NewsItem news)
    {
        AdImage.Source = news.ImageUrl;
        AdTitle.Text = news.Title;
        AdLayout.IsVisible = true;
    }

    private string GetRelativeTime(DateTime publishedAt)
    {
        var delta = DateTime.Now - publishedAt;
        return delta.TotalDays >= 1 
            ? publishedAt.ToString("MMM dd")
            : delta.TotalHours >= 1 
                ? $"{(int)delta.TotalHours}h"
                : $"{(int)delta.TotalMinutes}m";
    }

    private string ExtractVideoThumbnail(string videoUrl)
    {
        // Extract thumbnail from video URL or use placeholder
        return "video_placeholder.jpg";
    }
}
```

### 4. Real Internet Images Data Provider

```csharp
// Services/NewsDataProvider.cs
public class NewsDataProvider
{
    private static Random random = new Random();
    private long index = 0;
    
    private static string[] authorNames = new string[]
    {
        "Alex Chen", "Sarah Williams", "Mike Johnson", "Emma Davis", "Chris Brown",
        "Lisa Martinez", "David Wilson", "Amy Garcia", "Tom Anderson", "Maya Patel"
    };
    
    private static string[] postTexts = new string[]
    {
        "Just finished an amazing project! 🚀 Feeling accomplished and ready for the next challenge.",
        "Beautiful morning coffee and some deep thoughts about technology's future ☕️",
        "Working on something exciting. Can't wait to share it with everyone soon! 🎉",
        "Loved this book recommendation from a friend. Anyone else read it? 📚",
        "Amazing sunset from my balcony today. Nature never fails to inspire 🌅"
    };
    
    private static string[] articleTitles = new string[]
    {
        "Breaking: Revolutionary AI Technology Unveiled",
        "Climate Scientists Make Groundbreaking Discovery",
        "Tech Giants Announce Major Collaboration", 
        "New Study Reveals Surprising Health Benefits",
        "Space Mission Returns with Fascinating Data"
    };
    
    private static string[] articleDescriptions = new string[]
    {
        "Researchers have developed a new method that could change everything we know...",
        "The implications of this discovery could reshape our understanding of...",
        "Industry experts are calling this the most significant development in...",
        "Scientists from leading universities collaborated to uncover...",
        "This breakthrough opens up possibilities that were previously unimaginable..."
    };

    public List<NewsItem> GetNewsFeed(int count)
    {
        var items = new List<NewsItem>();
        
        for (int i = 0; i < count; i++)
        {
            index++;
            var newsType = GetRandomNewsType();
            
            var item = new NewsItem
            {
                Id = index,
                Type = newsType,
                AuthorName = GetRandomAuthor(),
                PublishedAt = DateTime.Now.AddMinutes(-random.Next(1, 1440)) // Random time within last day
            };
            
            ConfigureItemByType(item);
            items.Add(item);
        }
        
        return items;
    }
    
    private void ConfigureItemByType(NewsItem item)
    {
        switch (item.Type)
        {
            case NewsType.Text:
                item.Content = postTexts[random.Next(postTexts.Length)];
                break;
                
            case NewsType.Image:
                item.Content = postTexts[random.Next(postTexts.Length)];
                // High-quality random images from Picsum
                item.ImageUrl = $"https://picsum.photos/seed/{index}/600/400";
                break;
                
            case NewsType.Video:
                item.Title = "Amazing Video Content";
                item.Content = "Check out this incredible footage!";
                // Video thumbnail from Picsum
                item.VideoUrl = $"https://picsum.photos/seed/{index}/600/400";
                break;
                
            case NewsType.Article:
                item.Title = articleTitles[random.Next(articleTitles.Length)];
                item.Content = articleDescriptions[random.Next(articleDescriptions.Length)];
                item.ImageUrl = $"https://picsum.photos/seed/{index}/400/300";
                item.ArticleUrl = "https://example.com/article";
                break;
                
            case NewsType.Ad:
                item.Title = "Special Offer - Don't Miss Out!";
                item.Content = "Limited time offer on premium features";
                item.ImageUrl = $"https://picsum.photos/seed/{index}/600/200";
                break;
        }
        
        // Random engagement numbers
        item.LikesCount = random.Next(0, 1000);
        item.CommentsCount = random.Next(0, 150);
    }
    
    private NewsType GetRandomNewsType()
    {
        // Weighted distribution for realistic feed
        var typeWeights = new (NewsType type, int weight)[]
        {
            (NewsType.Text, 30),    // 30% text posts
            (NewsType.Image, 40),   // 40% image posts  
            (NewsType.Video, 15),   // 15% videos
            (NewsType.Article, 10), // 10% articles
            (NewsType.Ad, 5)        // 5% ads
        };
        
        var totalWeight = typeWeights.Sum(x => x.weight);
        var randomValue = random.Next(totalWeight);
        
        var currentWeight = 0;
        foreach (var (type, weight) in typeWeights)
        {
            currentWeight += weight;
            if (randomValue < currentWeight)
                return type;
        }
        
        return NewsType.Text;
    }
    
    private string GetRandomAuthor()
    {
        return authorNames[random.Next(authorNames.Length)];
    }
}
```

### 5. Feed Implementation with Real Data and Image Preloading

```xml
<!-- MainPage.xaml -->
<draw:Canvas>
    <draw:SkiaScroll
        x:Name="NewsScroll"
        Orientation="Vertical"
        RefreshCommand="{Binding RefreshCommand}"
        LoadMoreCommand="{Binding LoadMoreCommand}"
        LoadMoreOffset="200"
        RefreshEnabled="True"
        HorizontalOptions="Fill"
        VerticalOptions="Fill">
        
        <!-- Dynamic height cells using SkiaLayout with ItemTemplate -->
        <draw:SkiaLayout
            x:Name="NewsStack"
            Type="Column"
            ItemsSource="{Binding NewsItems}"
            ItemTemplate="{x:Type views:NewsCell}"
            Spacing="8"
            HorizontalOptions="Fill" />
            
    </draw:SkiaScroll>
</draw:Canvas>
```

```csharp
// NewsViewModel.cs
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
    
    private async Task RefreshFeed()
    {
        if (IsBusy) return;
        
        IsBusy = true;
        
        try
        {
            // Cancel previous preloading
            _preloadCancellation?.Cancel();
            
            // Generate fresh content
            var newItems = _dataProvider.GetNewsFeed(20);
            
            // Preload images in background (DrawnUI's SkiaImageManager)
            _preloadCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            _ = PreloadImages(newItems, _preloadCancellation.Token);
            
            // Update UI
            MainThread.BeginInvokeOnMainThread(() =>
            {
                NewsItems.Clear();
                NewsItems.AddRange(newItems);
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
            var newItems = _dataProvider.GetNewsFeed(15);
            
            // Preload new images
            _preloadCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            _ = PreloadImages(newItems, _preloadCancellation.Token);
            
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
            var imageUrls = items
                .Where(x => !string.IsNullOrEmpty(x.ImageUrl))
                .Select(x => x.ImageUrl)
                .ToList();
                
            // Use DrawnUI's image manager for efficient preloading
            await SkiaImageManager.Instance.PreloadImages(imageUrls, cancellationToken);
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

// MainPage.xaml.cs
public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = new NewsViewModel();
    }
}
```

## Key Advantages

### 1. **Perfect Recycling**
- Single cell type = maximum recycling efficiency
- No template switching overhead
- Consistent memory usage

### 2. **Dynamic Height Calculation**
DrawnUI automatically calculates heights based on visible content:

```csharp
// Height adjusts automatically based on:
// - Visible content elements (some hidden, some shown)
// - Text wrapping and line counts
// - Image aspect ratios and sizes
// - Content type variations
// - No manual height calculations needed!

// Example: Text post = ~120dp, Image post = ~320dp, Video = ~320dp, Article = ~180dp
// All calculated automatically by the layout system
```

### 3. **Simplified Maintenance**
- One cell to maintain vs 5+ templates
- Consistent styling across all content types
- Easy to add new content types

### 4. **Performance Benefits**
- Efficient SkiaSharp rendering with hardware acceleration
- Real internet image loading with background preloading
- Minimal GC pressure during scrolling (no allocations in hot path)
- Perfect cell recycling = consistent 60fps scrolling

### 5. **Real-World Ready**
- Uses `https://picsum.photos/` for high-quality random images  
- Includes `SkiaImageManager.Instance.PreloadImages()` for smooth scrolling
- Pull-to-refresh and infinite scrolling support
- Realistic weighted content distribution (40% images, 30% text, etc.)

## Conclusion: Just Draw What You Want

DrawnUI gives you the freedom to **just draw what you need**. One cell handles everything:

- **One universal cell** that adapts to any content type
- **Real internet images** loaded efficiently with preloading
- **Automatic height calculation** without manual measurement  
- **Seamless recycling** handled automatically
- **Consistent styling** across all content types
- **Smooth scrolling** even with dynamic heights

Adding a new content type? Simply add an enum value and a configuration method. No new templates needed.

The result? A smooth, efficient news feed that loads real images from the internet and gives you the freedom to just draw what you want. 🚀