using Microsoft.Extensions.Logging;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace BioGuard.Api.Services;

public class FirebasePushNotificationService : IPushNotificationService
{
    private readonly ILogger<FirebasePushNotificationService> _logger;
    private readonly bool _inicializado;

    public FirebasePushNotificationService(IConfiguration config, ILogger<FirebasePushNotificationService> logger)
    {
        _logger = logger;
        _inicializado = InicializarFirebase(config);
    }

    private bool InicializarFirebase(IConfiguration config)
    {
        try
        {
            var serviceAccountJson = config["Firebase:ServiceAccountJson"]
                ?? Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON");
            var credPath = config["Firebase:CredentialsPath"]
                ?? Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_PATH");

            if (!string.IsNullOrWhiteSpace(serviceAccountJson))
            {
                if (FirebaseApp.DefaultInstance == null)
                {
                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = CredentialFactory.FromJson<GoogleCredential>(serviceAccountJson)
                    });
                }
                _logger.LogInformation("Firebase Admin SDK initialized from JSON");
                return true;
            }

            if (!string.IsNullOrWhiteSpace(credPath) && File.Exists(credPath))
            {
                if (FirebaseApp.DefaultInstance == null)
                {
                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = CredentialFactory.FromFile<GoogleCredential>(credPath)
                    });
                }
                _logger.LogInformation("Firebase Admin SDK initialized from file: {Path}", credPath);
                return true;
            }

            _logger.LogWarning("Firebase credentials not configured. FCM disabled.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Firebase Admin SDK");
            return false;
        }
    }

    public async Task<bool> SendAsync(string token, string titulo, string cuerpo, Dictionary<string, string>? datos = null, bool altaPrioridad = false)
    {
        if (!_inicializado || string.IsNullOrWhiteSpace(token)) return false;

        try
        {
            var mensaje = new Message
            {
                Fid = token,
                Notification = new Notification { Title = titulo, Body = cuerpo },
                Data = datos ?? new Dictionary<string, string>(),
                Android = new AndroidConfig
                {
                    Priority = altaPrioridad ? Priority.High : Priority.Normal,
                    Notification = new AndroidNotification
                    {
                        ChannelId = altaPrioridad ? "alertas_criticas" : "alertas_preventivas",
                        DefaultSound = altaPrioridad,
                        Sound = altaPrioridad ? "alarm_sound" : "default"
                    }
                },
                Apns = new ApnsConfig
                {
                    Aps = new Aps
                    {
                        Alert = new ApsAlert { Title = titulo, Body = cuerpo },
                        Sound = altaPrioridad ? "critical" : "default",
                        ContentAvailable = true,
                        Category = altaPrioridad ? "ALERTA_CRITICA" : "ALERTA_PREVENTIVA"
                    }
                }
            };

            var response = await FirebaseMessaging.DefaultInstance.SendAsync(mensaje);
            _logger.LogInformation("FCM sent: {MessageId}", response);
            return true;
        }
        catch (FirebaseMessagingException)
        {
            _logger.LogWarning("FCM token not registered");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending FCM notification");
            return false;
        }
    }

    public async Task<int> SendMulticastAsync(List<string> tokens, string titulo, string cuerpo, Dictionary<string, string>? datos = null, bool altaPrioridad = false)
    {
        if (!_inicializado || tokens == null || tokens.Count == 0) return 0;

        try
        {
            var mensaje = new MulticastMessage
            {
                Fids = tokens.Where(t => !string.IsNullOrWhiteSpace(t)).ToList(),
                Notification = new Notification { Title = titulo, Body = cuerpo },
                Data = datos ?? new Dictionary<string, string>(),
                Android = new AndroidConfig
                {
                    Priority = altaPrioridad ? Priority.High : Priority.Normal,
                    Notification = new AndroidNotification
                    {
                        ChannelId = altaPrioridad ? "alertas_criticas" : "alertas_preventivas"
                    }
                }
            };

            var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(mensaje);
            _logger.LogInformation("FCM multicast: {Success}/{Total}", response.SuccessCount, tokens.Count);
            return response.SuccessCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FCM multicast");
            return 0;
        }
    }

    public async Task<bool> SendToTopicAsync(string topic, string titulo, string cuerpo, Dictionary<string, string>? datos = null)
    {
        if (!_inicializado || string.IsNullOrWhiteSpace(topic)) return false;

        try
        {
            var mensaje = new Message
            {
                Topic = topic,
                Notification = new Notification { Title = titulo, Body = cuerpo },
                Data = datos ?? new Dictionary<string, string>()
            };

            var response = await FirebaseMessaging.DefaultInstance.SendAsync(mensaje);
            _logger.LogInformation("FCM topic {Topic} sent: {MessageId}", topic, response);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending FCM to topic {Topic}", topic);
            return false;
        }
    }
}
