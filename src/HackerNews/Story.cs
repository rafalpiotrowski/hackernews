namespace HackerNews;

/// <summary>
/// Represents a story from Hacker News
/// </summary>
/// <param name="title"></param>
/// <param name="uri"></param>
/// <param name="postedBy"></param>
/// <param name="score"></param>
/// <param name="time"></param>
/// <param name="commentCount"></param>
public sealed record StoryDto(string title, string uri, string postedBy, int score, string time, int commentCount);

/// <summary>
/// Story actor that is responsible for loading a story from Hacker News and stores it in memory
/// 
/// TODO: implement backoff and retry in case of error
/// </summary>
public sealed record Story : IAsyncDisposable
{
    private sealed record StoryJson(
        string Title,
        string Url,
        string By,
        int Score,
        long Time,
        long[]? Kids);

    private const string ITEM_REQUEST_URI = "item/{Id}.json?print=pretty";

    public StoryDto? Data { get; private set; }
    public long Id { get; }

    private readonly HttpClient _hackerNews;
    private readonly ILogger<Story> _logger;
    private readonly StoriesCacheService _storiesCacheService;
    private readonly CancellationTokenSource _cts = new();
    private const long LOADED = 1;
    private const long NOT_LOADED = 0;
    private const long LOADING = -1;
    private long _loadingStatus = NOT_LOADED;

    internal Story(
        long id,
        HttpClient hackerNews,
        ILogger<Story> logger,
        StoriesCacheService storiesCacheService)
    {
        Id = id;
        _hackerNews = hackerNews;
        _logger = logger;
        _storiesCacheService = storiesCacheService;
        var _ = Task.Run(async () => await LoadStoryAsync(), _cts.Token);
    }

    private async ValueTask LoadStoryAsync()
    {
        Interlocked.Exchange(ref _loadingStatus, LOADING);
        try
        {
            _logger.LogDebug("Loading story with id {Id}", Id);
            var story = await _hackerNews.GetFromJsonAsync<StoryJson>(ITEM_REQUEST_URI.Replace("{Id}", Id.ToString()), _cts.Token);
            if(story == null)
            {
                _logger.LogWarning("Story with id {Id} not found", Id);
                Interlocked.Exchange(ref _loadingStatus, NOT_LOADED);
                return;
            }
            _logger.LogDebug("Story with id {Id} found: {Title}", Id, story.Title);
            
            // map story json to our dto
            Data = new StoryDto(
                story.Title, 
                story.Url, 
                story.By, 
                story.Score, 
                DateTimeOffset.FromUnixTimeSeconds(story.Time).ToString("yyyy-MM-ddTHH:mm:sszzz"), 
                story.Kids == null ? 0 : story.Kids.Length);
            
            Interlocked.Exchange(ref _loadingStatus, LOADED);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error loading story with id {Id}", Id);

            // in case of error we could backoff and retry later, and keep retrying until we succeed
            // or the task is cancelled
        }
        finally 
        {
            if (!_cts.IsCancellationRequested)
                await _storiesCacheService.NotifyStoryLoaded(Id);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
    }

    internal bool IsLoaded() => Interlocked.Read(ref _loadingStatus) == LOADED;
}