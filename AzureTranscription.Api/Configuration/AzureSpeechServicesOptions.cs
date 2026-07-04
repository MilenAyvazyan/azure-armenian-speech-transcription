namespace AzureTranscription.Api.Configuration;

public class AzureSpeechServicesOptions
{
    public string Endpoint { get; set; } = "";

    public string Region { get; set; } = "";

    public string SubscriptionKey { get; set; } = "";
}