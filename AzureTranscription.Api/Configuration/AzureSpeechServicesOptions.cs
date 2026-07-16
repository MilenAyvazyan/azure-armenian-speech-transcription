namespace AzureTranscription.Api.Configuration;

public class AzureSpeechServicesOptions
{
    public string Endpoint { get; set; } = "";

    public string Region { get; set; } = "";

    public string SubscriptionKey { get; set; } = "";

    // Full "self" URL of the Whisper base model, exactly as returned by
    // Azure (e.g. https://eastus.api.cognitive.microsoft.com/speechtotext/v3.2/models/base/{id}).
    public string WhisperModelSelfUrl { get; set; } = "";

    // Full "self" URL of your trained Custom Speech model, exactly as shown
    // in Speech Studio / the models API. Note: custom model self URLs do
    // NOT follow the same "/models/base/{id}" pattern as base models —
    // copy this directly from Azure rather than constructing it manually.
    public string CustomSpeechModelSelfUrl { get; set; } = "";
}