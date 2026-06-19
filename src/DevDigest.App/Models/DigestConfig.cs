namespace DevDigest.App.Models;

public class DigestConfig
{
    public DigestSettings Digest { get; set; } = new();
    public AnthropicSettings Anthropic { get; set; } = new();
    public EmailSettings Email { get; set; } = new();
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

public class AnthropicSettings
{
    public string ApiKey { get; set; } = string.Empty;
}

public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}