using System.Net.Http.Json;
using System.ServiceModel.Syndication;
using System.Xml;
using DevDigest.App.Models;
using Microsoft.Extensions.Logging;

namespace DevDigest.App.Services;

public interface IFeedFetcherService
{
    Task<List<FeedItem>> FetchFeedsAsync(CategoryConfig category, CancellationToken cancellationToken = default);
}

public class FeedFetcherService : IFeedFetcherService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FeedFetcherService> _logger;

    public FeedFetcherService(HttpClient httpClient, ILogger<FeedFetcherService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<FeedItem>> FetchFeedsAsync(CategoryConfig category, CancellationToken cancellationToken = default)
    {
        var items = new List<FeedItem>();

        foreach (var source in category.Sources)
        {
            try
            {
                if (source.Contains("hacker-news"))
                {
                    var hnItems = await FetchHackerNewsAsync(category.Name, cancellationToken);
                    items.AddRange(hnItems);
                }
                else
                {
                    var feedItems = await FetchRssFeedAsync(source, category.Name, cancellationToken);
                    items.AddRange(feedItems);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch from source: {Source}", source);
            }
        }

        return items.OrderByDescending(i => i.PublishedAt).ToList();
    }

    private async Task<List<FeedItem>> FetchRssFeedAsync(string url, string category, CancellationToken cancellationToken)
    {
        var items = new List<FeedItem>();

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        
        // Configure XmlReader to allow DTD processing for feeds that require it
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            ValidationType = ValidationType.None
        };
        
        using var reader = XmlReader.Create(stream, settings);

        var feed = SyndicationFeed.Load(reader);

        foreach (var item in feed.Items.Take(20))
        {
            items.Add(new FeedItem
            {
                Title = item.Title?.Text ?? "No Title",
                Url = item.Links.FirstOrDefault()?.Uri.ToString() ?? string.Empty,
                Summary = item.Summary?.Text ?? item.Content?.ToString() ?? string.Empty,
                Category = category,
                PublishedAt = item.PublishDate
            });
        }

        return items;
    }

    private async Task<List<FeedItem>> FetchHackerNewsAsync(string category, CancellationToken cancellationToken)
    {
        var items = new List<FeedItem>();

        var storyIds = await _httpClient.GetFromJsonAsync<List<int>>(
            "https://hacker-news.firebaseio.com/v0/topstories.json",
            cancellationToken);

        if (storyIds == null) return items;

        foreach (var id in storyIds.Take(20))
        {
            try
            {
                var story = await _httpClient.GetFromJsonAsync<HackerNewsItem>(
                    $"https://hacker-news.firebaseio.com/v0/item/{id}.json",
                    cancellationToken);

                if (story != null && !string.IsNullOrEmpty(story.Url))
                {
                    items.Add(new FeedItem
                    {
                        Title = story.Title ?? "No Title",
                        Url = story.Url ?? $"https://news.ycombinator.com/item?id={id}",
                        Summary = story.Text ?? string.Empty,
                        Category = category,
                        PublishedAt = story.Time.HasValue 
                            ? DateTimeOffset.FromUnixTimeSeconds(story.Time.Value) 
                            : DateTimeOffset.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch Hacker News story {Id}", id);
            }
        }

        return items;
    }

    private class HackerNewsItem
    {
        public int? Id { get; set; }
        public string? Title { get; set; }
        public string? Url { get; set; }
        public string? Text { get; set; }
        public long? Time { get; set; }
    }
}