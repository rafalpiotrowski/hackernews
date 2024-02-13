using System.Threading.Channels;

namespace HackerNews;

public sealed class BestStoriesService
{
    public static readonly string HACKER_NEWS_HTTP_CLIENT = "HackerNews";
    private const string BEST_STORIES_REQUEST_URI = "beststories.json";
    private readonly StoriesCacheService _cache;
    private readonly ILogger<BestStoriesService> _logger;
    private readonly HttpClient _hackerNews;

    public BestStoriesService(
        ILogger<BestStoriesService> logger,
        IHttpClientFactory httpClientFactory,
        StoriesCacheService storyLoader)
    {
        _logger = logger;
        _hackerNews = httpClientFactory.CreateClient(HACKER_NEWS_HTTP_CLIENT);
        _cache = storyLoader;
    }

    public async Task<StoryDto[]> GetBestStoriesAsync(int qty, CancellationToken cancellationToken = default)
    {
        if (qty <= 0) {
            return Array.Empty<StoryDto>();
        }
        // we allow for each request to call hackernews api and get the latest best stories
        var ids = await _hackerNews.GetFromJsonAsync<long[]>(BEST_STORIES_REQUEST_URI, cancellationToken);
        if(ids == null || ids.Length == 0) {
            _logger.LogWarning("No best stories found");
            return Array.Empty<StoryDto>();
        }
        
        var stories = Channel.CreateBounded<StoryDto[]>(1);
        await _cache.GetBestStoriesAsync(ids, (uint)qty, stories.Writer);
        return await stories.Reader.ReadAsync(cancellationToken);
    }
}





