namespace BioGuard.Api.Config;

public class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";
    public string AccessToken { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public bool SandboxMode { get; set; } = true;
}
