namespace BioGuard.Api.Config;

public class FirebaseOptions
{
    public const string SectionName = "Firebase";
    public string ServiceAccountJson { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string CredentialsPath { get; set; } = string.Empty;
}
