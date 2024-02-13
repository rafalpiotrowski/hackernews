using System.Threading.Channels;

namespace HackerNews;

/// <summary>
/// Service that caches stories from Hacker News
/// 
/// When a request for best stories is made, it will return the stories that are already loaded
/// otherwise it will stash the request and wait for the stories to be loaded.
/// 
/// When a story is loaded, it will check if all stories are loaded and if so, it will process the stashed requests.
/// </summary>
public sealed class StoriesCacheService : IAsyncDisposable
{
    private interface ICommand;
    private sealed record GetBestStoriesCommand(long[] BestStoriesIds, uint Count, ChannelWriter<StoryDto[]> Sender) : ICommand;
    private sealed record StoryLoaded(long Id) : ICommand;

    private readonly Channel<ICommand> _channel = Channel.CreateUnbounded<ICommand>(new UnboundedChannelOptions() {
        SingleReader = true,
        SingleWriter = false,
    });
    private readonly HttpClient _hackerNews;
    private readonly ILogger<StoriesCacheService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<long, Story> _stories = new();
    private readonly Queue<GetBestStoriesCommand> _stash = new();
    private readonly Task _backgroundTask;

    public StoriesCacheService(
        IHttpClientFactory httpClientFactory,
        ILogger<StoriesCacheService> logger,
        ILoggerFactory loggerFactory)
    {
        _hackerNews = httpClientFactory.CreateClient(BestStoriesService.HACKER_NEWS_HTTP_CLIENT);
        _logger = logger;
        _loggerFactory = loggerFactory;
        _backgroundTask = Task.Factory.StartNew(() => ExecuteAsync(_cts.Token), TaskCreationOptions.LongRunning);
    }

    public ValueTask GetBestStoriesAsync(long[] ids, uint count, ChannelWriter<StoryDto[]> sender) 
        => _channel.Writer.WriteAsync(new GetBestStoriesCommand(ids, count, sender));

    public ValueTask NotifyStoryLoaded(long id) => _channel.Writer.WriteAsync(new StoryLoaded(id));

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
    }

    private async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try 
        {
            _logger.LogInformation("StoriesCacheService is starting");

            while(!stoppingToken.IsCancellationRequested)
            {
                await foreach(var command in _channel.Reader.ReadAllAsync(stoppingToken))
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    _logger.LogInformation($"Processing command {command.GetType().Name}");
                    switch(command)
                    {
                        case GetBestStoriesCommand getBestStoriesCommand:
                            await HandleGetBestStoriesCommand(getBestStoriesCommand);
                            break;
                        case StoryLoaded storyLoadedCommand:
                            await HandleStoryLoadedCommand(storyLoadedCommand.Id);
                            break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("StoriesCacheService is stopping");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Error in StoriesCacheService");
        }
        finally
        {
            // cleanup
            foreach(var story in _stories.Values)
            {
                await story.DisposeAsync();
            }
        }

        _logger.LogInformation("StoriesCacheService is completed");
    }

    private async ValueTask HandleGetBestStoriesCommand(GetBestStoriesCommand cmd)
    {
        await UpdateBestStories(cmd.BestStoriesIds);

        int storyCount = _stories.Values.Count(story => !story.IsLoaded());
        if (storyCount - 1 > 0) 
        {
            //stash the request
            _logger.LogInformation($"Stashing GetBestStoriesCommand. Remaining: {storyCount}");
            _stash.Enqueue(cmd);
        }
        else 
        {
            _logger.LogDebug("All stories loaded. Processing GetBestStoriesCommand");
            // we have all stories loaded
            await SendStories(cmd.Count, cmd.Sender);
        }
    }

    private async ValueTask UpdateBestStories(long[] ids)
    {
        // we need to find out which stories are new and which are old
        foreach (var id in ids.Except(_stories.Keys))
        {
            _stories.Add(id, new Story(id, _hackerNews, _loggerFactory.CreateLogger<Story>(), this));
            _logger.LogDebug("Story with id {id} added", id);
        }
        foreach (var id in _stories.Keys.Except(ids))
        {
            if(_stories.Remove(id, out var story))
            {
                await story.DisposeAsync();
                _logger.LogDebug("Story with id {id} removed", id);
            }
        }
    }

    private async ValueTask SendStories(uint count, ChannelWriter<StoryDto[]> sender)
    {
        var stories = _stories.Values
            .Where(s => s.Data != null)
            .Select(s => s.Data!)
            .OrderByDescending(s => s.score)
            .Take((int)count);
        await sender.WriteAsync(stories.ToArray());
    }

    private async ValueTask HandleStoryLoadedCommand(long id)
    {
        int storyCount = _stories.Values.Count(story => !story.IsLoaded());
        if (storyCount - 1 > 0) 
        {
            _logger.LogDebug("Story {id} loaded. Remaining: {Story}: Stash count: {Stash}", id, storyCount, _stash.Count);
            return;
        }

        _logger.LogDebug("All stories loaded");
        while (_stash.TryDequeue(out var cmd))
        {
            await SendStories(cmd.Count, cmd.Sender);
        }
    }
}

