namespace DevDigest.App.Models;

public class DigestConfig
{
    public DigestSettings Digest { get; set; } = new();
    public OpenAISettings OpenAI { get; set; } = new();
    public SendGridSettings SendGrid { get; set; } = new();
}

public class DigestSettings
{
    public string ToEmail { get; set; } = string.Empty;
    public int MaxItemsPerCategory { get; set; } = 10;
    public List<CategoryConfig> Categories { get; set; } = new();
}

public class CategoryConfig
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public List<string> Sources { get; set; } = new();
}

public class OpenAISettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
}

public class SendGridSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
}