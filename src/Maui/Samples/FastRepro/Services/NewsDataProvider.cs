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
