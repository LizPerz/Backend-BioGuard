namespace BioGuard.Api.Config;

public class MLOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 15;
}
