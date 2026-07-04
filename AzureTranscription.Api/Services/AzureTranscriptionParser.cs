using System.Text.Json;
using AzureTranscription.Api.DTOs;

namespace AzureTranscription.Api.Services;

public class AzureTranscriptionParser
{
    public TranscriptionResultDto Parse(string json)
    {
        var result = new TranscriptionResultDto();

        using JsonDocument document = JsonDocument.Parse(json);

        // Azure parsing will be implemented here

        return result;
    }
}