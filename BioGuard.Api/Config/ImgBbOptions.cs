namespace BioGuard.Api.Config;

public class ImgBbOptions
{
    public const string SectionName = "ImgBB";
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.imgbb.com/1/upload";
    public int MaxFileSizeMb { get; set; } = 5;
}
