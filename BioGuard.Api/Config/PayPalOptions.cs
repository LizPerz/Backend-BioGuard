namespace BioGuard.Api.Config;

public class PayPalOptions
{
    public const string SectionName = "PayPal";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string WebhookId { get; set; } = string.Empty;
    public bool SandboxMode { get; set; } = true;
}
