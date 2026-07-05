namespace AzureTranscription.Api.DTOs;

public class TranscriptionUtteranceDto
{
    public string Speaker { get; set; } = "";

    public string Text { get; set; } = "";

    public TimeSpan Offset { get; set; }

    public TimeSpan Duration { get; set; }
}