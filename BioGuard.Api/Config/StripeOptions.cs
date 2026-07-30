namespace BioGuard.Api.Config;

public class StripeOptions
{
    public const string SectionName = "Stripe";
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string PriceIdGratis { get; set; } = string.Empty;
    public string PriceIdPlus { get; set; } = string.Empty;
    public string PriceIdCare { get; set; } = string.Empty;
    public string PriceIdFamily { get; set; } = string.Empty;
    public string PriceIdProSalud { get; set; } = string.Empty;
}
