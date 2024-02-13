using Microsoft.AspNetCore.Mvc;

namespace HackerNews;

[ApiController]
[Route("/")]
public sealed class BestStoriesController : ControllerBase 
{
    private readonly ILogger<BestStoriesController> _logger;
    private readonly BestStoriesService _bestStoriesService;

    public BestStoriesController(
        ILogger<BestStoriesController> logger,
        BestStoriesService bestStoriesService)
    {
        _logger = logger;
        _bestStoriesService = bestStoriesService;
    }

    [HttpGet("{nrOfStoriesToLoad}")]
    [ProducesResponseType<StoryDto[]>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StoryDto[]>> Get(int nrOfStoriesToLoad, CancellationToken cancellationToken) 
    {
        _logger.LogInformation($"Getting {nrOfStoriesToLoad} best stories");
        return Ok(await _bestStoriesService.GetBestStoriesAsync(nrOfStoriesToLoad, cancellationToken));
    }
}