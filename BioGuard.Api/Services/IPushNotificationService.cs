namespace BioGuard.Api.Services;

public interface IPushNotificationService
{
    Task<bool> SendAsync(string token, string titulo, string cuerpo, Dictionary<string, string>? datos = null, bool altaPrioridad = false);
    Task<int> SendMulticastAsync(List<string> tokens, string titulo, string cuerpo, Dictionary<string, string>? datos = null, bool altaPrioridad = false);
    Task<bool> SendToTopicAsync(string topic, string titulo, string cuerpo, Dictionary<string, string>? datos = null);
}
