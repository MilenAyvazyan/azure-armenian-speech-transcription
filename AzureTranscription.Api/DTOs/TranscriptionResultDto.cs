namespace AzureTranscription.Api.DTOs;

public class TranscriptionResultDto
{
    public string JobId { get; set; } = "";

    public string Status { get; set; } = "";

    public List<TranscriptionUtteranceDto> Utterances { get; set; }
        = new();
}