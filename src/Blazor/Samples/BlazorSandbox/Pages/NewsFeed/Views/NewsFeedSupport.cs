using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AppoMobi.Specials;
using DrawnUi.Draw;
using DrawnUi.Draw.ApplicationModel;

namespace BlazorSandbox.Pages.NewsFeed;

public class BaseViewModel : INotifyPropertyChanged
{
    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

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
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string ArticleUrl { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorAvatarUrl { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public int LikesCount { get; set; }
    public int CommentsCount { get; set; }
}

public class NewsDataProvider
{
    private static readonly Random Random = new();
    private long _index;

    private static readonly (string name, string avatarUrl)[] Authors =
    [
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
    ];

    private static readonly string[] PostTexts =
    [
        "Just finished an amazing project! 🚀 Feeling accomplished and ready for the next challenge.",
        "Beautiful morning coffee and some deep thoughts about technology's future ☕️",
        "Working on something exciting. Can't wait to share it with everyone soon! 🎉",
        "Loved this book recommendation from a friend. Anyone else read it? 📚",
        "Amazing sunset from my balcony today. Nature never fails to inspire 🌅"
    ];

    private static readonly string[] ArticleTitles =
    [
        "Breaking: Revolutionary AI Technology Unveiled",
        "Climate Scientists Make Groundbreaking Discovery",
        "Tech Giants Announce Major Collaboration",
        "New Study Reveals Surprising Health Benefits",
        "Space Mission Returns with Fascinating Data"
    ];

    private static readonly string[] ArticleDescriptions =
    [
        "Researchers have developed a new method that could change everything we know...",
        "The implications of this discovery could reshape our understanding of...",
        "Industry experts are calling this the most significant development in...",
        "Scientists from leading universities collaborated to uncover...",
        "This breakthrough opens up possibilities that were previously unimaginable..."
    ];

    public List<NewsItem> GetNewsFeed(int count)
    {
        var items = new List<NewsItem>();

        for (var i = 0; i < count; i++)
        {
            _index++;
            var author = GetRandomAuthor();
            var item = new NewsItem
            {
                Id = _index,
                Type = GetRandomNewsType(),
                AuthorName = author.name,
                AuthorAvatarUrl = author.avatarUrl,
                PublishedAt = DateTime.Now.AddMinutes(-Random.Next(1, 1440))
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
                item.Content = PostTexts[Random.Next(PostTexts.Length)];
                break;
            case NewsType.Image:
                item.Content = PostTexts[Random.Next(PostTexts.Length)];
                item.ImageUrl = $"https://picsum.photos/seed/{_index}/600/400";
                break;
            case NewsType.Video:
                item.Title = "Amazing Video Content";
                item.Content = "Check out this incredible footage!";
                item.VideoUrl = $"https://picsum.photos/seed/{_index}/600/400";
                break;
            case NewsType.Article:
                item.Title = ArticleTitles[Random.Next(ArticleTitles.Length)];
                item.Content = ArticleDescriptions[Random.Next(ArticleDescriptions.Length)];
                item.ImageUrl = $"https://picsum.photos/seed/{_index}/400/300";
                item.ArticleUrl = "https://example.com/article";
                break;
            case NewsType.Ad:
                item.Title = "Special Offer - Don't Miss Out!";
                item.Content = "Limited time offer on premium features";
                item.ImageUrl = $"https://picsum.photos/seed/{_index}/600/200";
                break;
        }

        item.LikesCount = Random.Next(0, 1000);
        item.CommentsCount = Random.Next(0, 150);
    }

    private static NewsType GetRandomNewsType()
    {
        var typeWeights = new (NewsType type, int weight)[]
        {
            (NewsType.Text, 30),
            (NewsType.Image, 40),
            (NewsType.Video, 15),
            (NewsType.Article, 10),
            (NewsType.Ad, 5)
        };

        var totalWeight = typeWeights.Sum(x => x.weight);
        var randomValue = Random.Next(totalWeight);

        var currentWeight = 0;
        foreach (var (type, weight) in typeWeights)
        {
            currentWeight += weight;
            if (randomValue < currentWeight)
                return type;
        }

        return NewsType.Text;
    }

    private static (string name, string avatarUrl) GetRandomAuthor()
    {
        return Authors[Random.Next(Authors.Length)];
    }
}

public class NewsViewModel : BaseViewModel
{
    private readonly NewsDataProvider _dataProvider;
    private CancellationTokenSource? _preloadCancellation;
    private bool _isRefreshing;
    private const int DataChunkSize = 50;

    public NewsViewModel()
    {
        _dataProvider = new NewsDataProvider();
        NewsItems = new ObservableRangeCollection<NewsItem>();

        RefreshCommand = new Command(async () => await RefreshFeed(1500));
        LoadMoreCommand = new Command(async () => await LoadMore());

        Tasks.StartDelayed(TimeSpan.FromMilliseconds(50), async () =>
        {
            await RefreshFeed(10);
        });
    }

    public ObservableRangeCollection<NewsItem> NewsItems { get; }

    public ICommand RefreshCommand { get; }
    public ICommand LoadMoreCommand { get; }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            if (_isRefreshing != value)
            {
                _isRefreshing = value;
                OnPropertyChanged();
            }
        }
    }

    private async Task RefreshFeed(int msDelay)
    {
        if (IsBusy)
            return;

        IsBusy = true;

        await Task.Delay(msDelay);

        try
        {
            _preloadCancellation?.Cancel();

            Debug.WriteLine("Loading news feed !!!");

            var newItems = _dataProvider.GetNewsFeed(DataChunkSize);

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
            IsRefreshing = false;
        }
    }

    private async Task LoadMore()
    {
        if (IsBusy)
            return;

        IsBusy = true;

        try
        {
            Debug.WriteLine("Loading more items !!!");
            var newItems = _dataProvider.GetNewsFeed(DataChunkSize / 2);

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