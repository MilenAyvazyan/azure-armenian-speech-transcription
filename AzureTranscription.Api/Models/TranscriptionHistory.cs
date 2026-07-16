using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace AzureTranscription.Api.Models
{
    public class TranscriptionHistory
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string FileName { get; set; } = null!;
        public string AudioUrl { get; set; } = null!;
        public string Text { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public string? AzureJobUrl { get; set; }
        public string Status { get; set; } = "Processing";

        // "Whisper" or "CustomSpeech" — tells the frontend which model produced this record.
        public string? ModelUsed { get; set; }

        // Shared across the two records created from the same audio upload,
        // so the frontend can group and display them side by side.
        public string? GroupId { get; set; }

        public List<UtteranceRecord> Utterances { get; set; } = new();
    }

    public class UtteranceRecord
    {
        public string Speaker { get; set; } = "";
        public string Text { get; set; } = "";
    }
}