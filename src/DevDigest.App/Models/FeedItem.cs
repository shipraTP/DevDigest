namespace DevDigest.App.Models;

public class FeedItem
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; set; }
}