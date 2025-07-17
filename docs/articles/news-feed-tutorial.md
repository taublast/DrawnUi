---
title: News Feed Scroller Tutorial
description: When MAUI CollectionView is not enough.. \r\nThink Drawn! DrawnUI News Feed Scroller Tutorial.
categories: [MAUI, DrawnUI]
tags: [drawnui, skiasharp, dotnetmaui]    
image: /images/scroller.jpg
---
# News Feed Scroller Tutorial: One Cell to Rule Them All

Building a news feed with mixed content types (text posts, images, videos, articles, ads) is a common requirement. With DrawnUI, you get the freedom to **just draw what you need** inside a recycled cell! 🎨

## 🚀 Live Demo in Tutorials Project

Want to see this in action first? Check out the [**DrawnUI Tutorials Project**](https://github.com/taublast/DrawnUi.Maui/tree/main/src/Maui/Samples/Tutorials) for:
- **News Feed Tutorial** - This tutorial in action with live scrolling
- **Interactive Cards (XAML)** - Beautiful animated cards
- **Interactive Cards (Code)** - Same UI built with C# code-behind  
- **First App** - Your first DrawnUI app
- **Button Tutorial** - Game-style interactive buttons

Clone the repo and run the Tutorials project to explore all examples!

## 🚀 This Tutorial Features:
* **📏 Uneven row heights** - because real content isn't uniform!
* **✨ Shadows behind cells** - adds visual depth to the interface
* **🌐 Real internet images** for avatars and banners from actual APIs
* **📊 Large dataset handling** - measures only visible items at startup, then works in background
* **♾️ Load more functionality** - you never know how far users will scroll!

## 🎓 What You'll Learn:
* **🏗️ Smart caching strategies** - organize layers to redraw only what changed
* **⚡ Performance optimization** - handle thousands of items smoothly
* **🔄 Recycling mastery** - one cell type handles all content variations
* **📱 Real-world techniques** - progressive measurement, smart caching, and more!
 
 <img src="../images/scroller.jpg" alt="News Feed Tutorial" width="350" style="margin-top: 16px;" />

## 🎯 The DrawnUI Way: One Universal Cell

With DrawnUI, we can use a layout as a cell that simply shows or hides elements based on content type - no complex `DataTemplateSelector` needed! All recycling and height calculation happen automatically ✨

## 🎯 What We Want to Build

A news feed with mixed content types (text posts, images, videos, articles, ads). We will be using a combination of `SkiaScroll` and `SkiaLayout` to obtain a recycled cells scrolling view. We will also use `SkiaDynamicDrawnCell` custom control as our cell base. This is optional - you could use any `SkiaControl` as your cell, but it's a helpful utility for handling BindingContext changes smoothly and provides useful override methods.

## ⚙️ The Tech Behind

`SkiaScroll` can scroll any content. When paired with a `SkiaLayout` it can communicate the viewport size/position to its child and retrieve some information back. With special properties `SkiaLayout` can act like a bindable item layout, and inside `SkiaScroll` it can show its full potential with recycling and virtualization! 💪

So what we will do is simply placing a SkiaLayout inside the scroll, defining an ItemTemplate and ItemsSource, plus setting some related properties.

Another important point is the databinding for the recycled view - the cell. We'll do it in code-behind for better performance. `SkiaDynamicDrawnCell` helper provides us with a `SetContent` method we can override to update the cell content based on the new BindingContext. This code is wrapped by the helper with a batch update lock, so no intermediate rendering happens. We could also override `ContextPropertyChanged` if we wanted to react to property changes in the bound object (for example `IsOnline` changing for a person and updating the avatar color to green), but we'll keep this tutorial simple.

We will be using real internet resources to get images for avatars and banners to be realistic with performance. We'll also be using shadow effects for visual appeal.
You can display debugging information over the scroll to see displayed/created/measured number of cells along with FPS.


## 🛠️ Step-by-Step Implementation

### 1. 📱 Setup The App

Proceed as described in the [Getting Started](getting-started.md) section. When working on desktop you'll normally want to set your app window to a phone-like size, to be consistent with mobile platforms:

```csharp
        .UseDrawnUi(new DrawnUiStartupSettings
        {
            DesktopWindow = new()
            {
                Width = 375,
                Height = 800
            }
        })
```

### 2. 📋 Define Content Types

```csharp
namespace Sandbox.Models;

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
    public long Id { get; set; }
    public NewsType Type { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public string ImageUrl { get; set; }
    public string VideoUrl { get; set; }
    public string ArticleUrl { get; set; }
    public string AuthorName { get; set; }
    public string AuthorAvatarUrl { get; set; }
    public DateTime PublishedAt { get; set; }
    public int LikesCount { get; set; }
    public int CommentsCount { get; set; }
}
```

### 3. 🏗️ Create Your Cell

> **Caching Strategy Note**: For recycled cells, `UseCache="ImageDoubleBuffered"` works well - it displays the previous cache while the next one is being prepared in background, allowing smooth scrolling.

> **Shadow Performance**: Shadows are cached in a separate background layer to avoid performance issues. The shadow layer is cached independently from the content.

> **Spacing Strategy**: Stack spacing is set to 0 because the cell margin/padding acts as general spacing between items. If we had no special layer for saving background with shadows you could use Spacing normally, but we need that space for shadows.

Normally you might want to use `SkiaSvg` for vector graphics or even `SkiaLottie` for animated icons, but for simplicity we'll be using emoji for this tutorial! 😊

```xml
<?xml version="1.0" encoding="utf-8"?>

<draw:SkiaDynamicDrawnCell
    x:Class="Sandbox.Views.NewsCell"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:draw="http://schemas.appomobi.com/drawnUi/2023/draw"
    HorizontalOptions="Fill"
    UseCache="ImageDoubleBuffered">

    <!--cached background layer with shadow-->
    <draw:SkiaLayout
        VerticalOptions="Fill"
        HorizontalOptions="Fill"
        UseCache="Image"
        Padding="16,6,16,10">

        <draw:SkiaShape
            CornerRadius="0"
            BackgroundColor="White"
            VerticalOptions="Fill"
            HorizontalOptions="Fill">
            <draw:SkiaShape.VisualEffects>
                <draw:DropShadowEffect
                    Color="#33000000" Blur="3" X="3" Y="3" />
            </draw:SkiaShape.VisualEffects>
        </draw:SkiaShape>
    </draw:SkiaLayout>

    <draw:SkiaLayout
        Margin="16,6,16,10"
        Padding="16"
        Type="Column" Spacing="12"
        HorizontalOptions="Fill">

              <!-- Author Header -->
        <draw:SkiaLayout Type="Row" Spacing="8"
                         UseCache="Image"
                         HorizontalOptions="Fill">

            <!--avatar image-->
            <draw:SkiaShape
                x:Name="AvatarFrame"
                Type="Circle"
                WidthRequest="40"
                HeightRequest="40"
                BackgroundColor="LightGray">

                <draw:SkiaImage
                    x:Name="AvatarImage"
                    Aspect="AspectFill"
                    HorizontalOptions="Fill"
                    VerticalOptions="Fill" />

            </draw:SkiaShape>

            <!--avatar initials-->
            <draw:SkiaLayout Type="Column"
                             UseCache="Operations"
                             HorizontalOptions="Fill">
                <draw:SkiaLabel
                    x:Name="AuthorLabel"
                    FontSize="14"
                    FontAttributes="Bold"
                    TextColor="Black" />
                <draw:SkiaLabel
                    x:Name="TimeLabel"
                    FontSize="12"
                    TextColor="Gray" />
            </draw:SkiaLayout>
        </draw:SkiaLayout>

        <!-- Content Title -->
        <draw:SkiaRichLabel
            UseCache="Operations"
            x:Name="TitleLabel"
            FontSize="16"
            FontAttributes="Bold"
            TextColor="Black"
            IsVisible="False" />

        <!-- Text Content -->
        <draw:SkiaRichLabel
            UseCache="Operations"
            x:Name="ContentLabel"
            FontSize="14"
            TextColor="#333333"
            LineBreakMode="WordWrap"
            IsVisible="False" />

        <!-- Image Content -->
        <draw:SkiaShape x:Name="ContentImage"
                        IsVisible="False"
                        HorizontalOptions="Fill"
                        HeightRequest="200">
            <draw:SkiaImage
                BackgroundColor="LightGray"
                x:Name="ContentImg"
                Aspect="AspectCover"
                VerticalOptions="Fill"
                HorizontalOptions="Fill" />
        </draw:SkiaShape>

        <!-- Video Thumbnail with Play Button -->
        <draw:SkiaLayout
            HorizontalOptions="Fill"
            UseCache="Image"
            x:Name="VideoLayout"
            Type="Absolute"
            HeightRequest="200"
            IsVisible="False">

            <draw:SkiaImage
                BackgroundColor="LightGray"
                x:Name="VideoThumbnail"
                Aspect="AspectFill"
                HorizontalOptions="Fill"
                VerticalOptions="Fill" />

            <!--wrapper to cache shadow-->
            <draw:SkiaLayout
                UseCache="Image"
                Padding="20"
                HorizontalOptions="Center"
                VerticalOptions="Center">

                <draw:SkiaShape
                    Type="Circle"
                    WidthRequest="60"
                    HeightRequest="60"
                    BackgroundColor="Black"
                    Opacity="0.7"
                    HorizontalOptions="Center"
                    VerticalOptions="Center">

                </draw:SkiaShape>

                <draw:SkiaRichLabel
                    Text="▶"
                    Opacity="0.7"
                    FontSize="26"
                    TextColor="White"
                    HorizontalOptions="Center"
                    VerticalOptions="Center" />

            </draw:SkiaLayout>

        </draw:SkiaLayout>

        <!-- Article Preview -->
        <draw:SkiaLayout
            HorizontalOptions="Fill"
            UseCache="Image"
            x:Name="ArticleLayout"
            Type="Row"
            Spacing="12"
            IsVisible="False">

            <draw:SkiaShape
                UseCache="Image"
                CornerRadius="8,0,0,8"
                WidthRequest="80"
                HeightRequest="80">
                <draw:SkiaImage
                    HorizontalOptions="Fill"
                    VerticalOptions="Fill"
                    BackgroundColor="LightGray"
                    x:Name="ArticleThumbnail"
                    Aspect="AspectCover" />
            </draw:SkiaShape>

            <draw:SkiaLayout Type="Column" HorizontalOptions="Fill" UseCache="Operations">
                <draw:SkiaLabel
                    x:Name="ArticleTitle"
                    FontSize="14"
                    FontAttributes="Bold"
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
            HeightRequest="150"
            BackgroundColor="LightGray"
            HorizontalOptions="Fill"
            UseCache="Image"
            x:Name="AdLayout"
            Type="Column"
            Spacing="8"
            IsVisible="False">

            <draw:SkiaLabel
                UseCache="Operations"
                Text="Sponsored"
                FontSize="10"
                TextColor="Gray"
                HorizontalOptions="End" />

            <draw:SkiaImage
                UseCache="Image"
                x:Name="AdImage"
                VerticalOptions="Fill"
                HorizontalOptions="Fill"
                Aspect="AspectFill" />

            <draw:SkiaLabel
                UseCache="Operations"
                x:Name="AdTitle"
                FontSize="14"
                Margin="8"
                FontAttributes="Bold"
                TextColor="Black" />

        </draw:SkiaLayout>

        <!-- Interaction Bar -->
        <draw:SkiaLayout Type="Row"
                         UseCache="Operations"
                         Spacing="16" HorizontalOptions="Fill">

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

    <draw:SkiaLabel
        Margin="32,24"
        x:Name="DebugId"
        TextColor="Red"
        HorizontalOptions="End" UseCache="Operations" />

</draw:SkiaDynamicDrawnCell>
```



```csharp
using DrawnUi.Controls;
using Sandbox.Models;

namespace Sandbox.Views;

public partial class NewsCell : SkiaDynamicDrawnCell
{
    public NewsCell()
    {
        InitializeComponent();
    }

    protected override void SetContent(object ctx)
    {
        base.SetContent(ctx);

        if (ctx is NewsItem news)
        {
            ConfigureForContentType(news);
        }
    }

    private void ConfigureForContentType(NewsItem news)
    {
        // Reset all content visibility
        HideAllContent();

        // Configure common elements
        DebugId.Text = $"{news.Id}";
        AuthorLabel.Text = news.AuthorName;
        TimeLabel.Text = GetRelativeTime(news.PublishedAt);
        AvatarImage.Source = news.AuthorAvatarUrl;
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
        return videoUrl; // For now, just use the same URL as it's from Picsum
    }
}
```

### 5. 🌐 Real Internet Images Data Provider

> **Real Avatar Images**: Uses RandomUser.me API for 100x100px professional avatars
> **Real Content Images**: Uses Picsum Photos API for high-quality random images

```csharp
using Sandbox.Models;

namespace Sandbox.Services;

public class NewsDataProvider
{
    private static Random random = new Random();
    private long index = 0;

    private static (string name, string avatarUrl)[] authors = new (string, string)[]
    {
        ("Alex Chen", "https://randomuser.me/api/portraits/men/1.jpg"),
        ("Sarah Williams", "https://randomuser.me/api/portraits/women/2.jpg"),
        ("Mike Johnson", "https://randomuser.me/api/portraits/men/3.jpg"),
        ("Emma Davis", "https://randomuser.me/api/portraits/women/4.jpg"),
        ("Chris Brown", "https://randomuser.me/api/portraits/men/5.jpg"),
        ("Lisa Martinez", "https://randomuser.me/api/portraits/women/6.jpg"),
        ("David Wilson", "https://randomuser.me/api/portraits/men/7.jpg"),
        ("Amy Garcia", "https://randomuser.me/api/portraits/women/8.jpg"),
        ("Tom Anderson", "https://randomuser.me/api/portraits/men/9.jpg"),
        ("Maya Patel", "https://randomuser.me/api/portraits/women/10.jpg")
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

            var author = GetRandomAuthor();
            var item = new NewsItem
            {
                Id = index,
                Type = newsType,
                AuthorName = author.name,
                AuthorAvatarUrl = author.avatarUrl,
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

    private (string name, string avatarUrl) GetRandomAuthor()
    {
        return authors[random.Next(authors.Length)];
    }
}
```

### 6. 🚀 Feed Implementation with Real Data

> **Spacing Strategy**: Stack spacing is 0 because cell margin/padding provides the spacing between items
> **Recycling**: RecyclingTemplate="Enabled" with experimental MeasureItemsStrategy="MeasureVisible" for optimal performance with large lists and uneven rows
> **Template Reservation**: ReserveTemplates="10" pre-allocates cell templates for smoother scrolling

```xml
<!-- TutorialNewsFeed.xaml excerpt -->
<draw:SkiaScroll
    x:Name="NewsScroll"
    Orientation="Vertical"
    FrictionScrolled="0.2"
    ChangeVelocityScrolled="0.9"
    RefreshCommand="{Binding RefreshCommand}"
    LoadMoreCommand="{Binding LoadMoreCommand}"
    RefreshEnabled="True"
    HorizontalOptions="Fill"
    VerticalOptions="Fill">

    <draw:SkiaScroll.Header>
        <draw:SkiaLayer HeightRequest="40" UseCache="Image">
            <draw:SkiaRichLabel
                Text="DrawnUI News Feed Tutorial"
                HorizontalOptions="Center" VerticalOptions="Center" />
        </draw:SkiaLayer>
    </draw:SkiaScroll.Header>

    <draw:SkiaScroll.Footer>
        <draw:SkiaLayer HeightRequest="50" />
    </draw:SkiaScroll.Footer>

    <!-- Dynamic height cells using SkiaLayout with ItemTemplate -->
    <draw:SkiaLayout
        x:Name="NewsStack"
        Type="Column"
        ReserveTemplates="10"
        ItemsSource="{Binding NewsItems}"
        RecyclingTemplate="Enabled"
        MeasureItemsStrategy="MeasureVisible"
        Spacing="0"
        HorizontalOptions="Fill">

        <draw:SkiaLayout.ItemTemplate>
            <DataTemplate>
                <views:NewsCell />
            </DataTemplate>
        </draw:SkiaLayout.ItemTemplate>

    </draw:SkiaLayout>

</draw:SkiaScroll>
```

```csharp
using System.Diagnostics;
using System.Windows.Input;
using AppoMobi.Specials;
using Sandbox.Models;
using Sandbox.Services;

namespace Sandbox.ViewModels;

public class NewsViewModel : BaseViewModel
{
    private readonly NewsDataProvider _dataProvider;

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
            Debug.WriteLine($"Loading news feed !!!");

            // Generate fresh content
            var newItems = _dataProvider.GetNewsFeed(20);

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
            var newItems = _dataProvider.GetNewsFeed(15);

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


}

// TutorialNewsFeed.xaml.cs
public partial class TutorialNewsFeed : DrawnUiBasePage
{
    public TutorialNewsFeed()
    {
        try
        {
            InitializeComponent();
            BindingContext = new NewsViewModel();
        }
        catch (Exception e)
        {
            Super.DisplayException(this, e);
        }
    }
}
```

## Key Advantages

### 1. **Recycling with Smart Caching**
- Single cell type provides good recycling efficiency
- `UseCache="ImageDoubleBuffered"` works well with MeasureVisible strategy
- Strategic cache placement: `UseCache="Operations"` for text, `UseCache="Image"` for complex layouts
- Separate shadow layer with independent caching for better performance

### 2. **Handling Uneven Row Heights**
This tutorial demonstrates the challenging case of **uneven row heights** using the experimental **MeasureVisible** strategy:

```csharp
// Different content types = different heights:
// - Text post = ~120dp
// - Image post = ~320dp
// - Video post = ~320dp
// - Article preview = ~180dp
// - Ad content = ~250dp
// All calculated automatically by DrawnUI's layout system
```

> **💡 MeasureVisible Strategy**: This experimental strategy works well for large lists with uneven rows. It measures only visible items initially, then progressively measures off-screen items in the background. This can provide good scrolling performance with thousands of items of varying heights.

### 3. **Real Internet Images**
- **Avatars**: RandomUser.me API (100x100px professional portraits)
- **Content**: Picsum Photos API (high-quality random images)
- **Caching**: DrawnUI's SkiaImageManager handles efficient image caching

### 4. **Shadow Performance Optimization**
- Shadows are cached in a separate background layer for optimal performance
- Background layer with `UseCache="Image"` contains shadow effects independently
- Cell margin/padding creates space for shadows to render properly

### 5. **Spacing Strategy**
- Stack `Spacing="0"` because cell margin/padding provides item spacing
- Cell `Margin="16,6,16,10"` and `Padding="16"` act as general spacing between items
- Separate background layer allows independent shadow caching

### 6. **LoadMore Implementation**
- **Refresh**: Uses `ReplaceRange()` to replace all items
- **LoadMore**: Uses `AddRange()` to append new items (proper infinite scroll)
- **ObservableRangeCollection**: Efficient bulk operations without individual notifications

### 7. **Smart Text Rendering**
- **SkiaLabel**: Fast rendering when your font family supports all symbols
- **SkiaRichLabel**: Auto-finds installed fonts for Unicode symbols (emojis, special characters)
- **No more "???"**: SkiaRichLabel prevents missing symbol fallbacks by finding the right fonts
- **Customizable fallbacks**: SkiaLabel lets you customize fallback symbols if needed

### 8. **Icons & Animations**
We kept it simple with emoji for this tutorial, but DrawnUI provides additional options:
- **SkiaSvg**: Vector icons for like, share, play buttons that scale well
- **SkiaLottie**: Animated icons (animated hearts, loading spinners, etc.)
- **Performance**: Both render with hardware acceleration and cache efficiently

## Conclusion: Just Draw What You Want

DrawnUI gives you the freedom to **just draw what you need**. This tutorial demonstrates a challenging real-world scenario:

### ✅ **What We Accomplished**
- **One universal cell** handling 5 different content types with uneven heights
- **Real internet images** from RandomUser.me (avatars) and Picsum Photos (content)
- **Smart caching strategy** using `UseCache="ImageDoubleBuffered"` with MeasureVisible
- **Shadow performance optimization** with separate cached background layer
- **Proper LoadMore** implementation with `AddRange()` vs `ReplaceRange()`
- **Strategic spacing** using cell margin/padding instead of stack spacing
- **Experimental MeasureVisible** strategy for optimal large list performance

### 🎯 **Performance Optimizations**
- **Caching**: `UseCache="ImageDoubleBuffered"` for cells, `UseCache="Image"` for layouts, `UseCache="Operations"` for text
- **Shadows**: Separate background layer with independent caching
- **Spacing**: Stack spacing = 0, cell margin/padding provides item spacing
- **Images**: DrawnUI's SkiaImageManager handles efficient image caching automatically
- **MeasureVisible**: Progressive measurement for instant scrolling with thousands of items

### 🚀 **The DrawnUI Advantage**
Adding a new content type? Simply add an enum value and a configuration method. No new templates, no complex selectors, no performance compromises.

The result? A smooth, efficient news feed that handles the challenging case of uneven row heights while loading real images from the internet. **Just draw what you want!** 🎨