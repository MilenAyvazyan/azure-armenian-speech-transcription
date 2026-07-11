using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

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
    }
}