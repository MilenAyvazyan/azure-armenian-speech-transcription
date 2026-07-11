using AzureTranscription.Api.Models;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AzureTranscription.Api.Services
{
    public interface IMongoService
    {
        Task SaveTranscriptionAsync(TranscriptionHistory history);
        Task<List<TranscriptionHistory>> GetAllHistoryAsync();
        Task<bool> DeleteByIdAsync(string id);
        Task<long> DeleteAllAsync();
        Task<string> CreateProcessingRecordAsync(string fileName, string azureJobUrl);
        Task UpdateResultByAzureJobUrlAsync(string azureJobUrl, string text, string status);
        Task<TranscriptionHistory?> GetByIdAsync(string id);
    }

    public class MongoService : IMongoService
    {
        private readonly IMongoCollection<TranscriptionHistory> _collection;

        public MongoService(IConfiguration configuration)
        {
            var client = new MongoClient(configuration["MongoDbSettings:ConnectionString"]);
            var database = client.GetDatabase(configuration["MongoDbSettings:DatabaseName"]);
            _collection = database.GetCollection<TranscriptionHistory>("Transcriptions");
        }

        public async Task SaveTranscriptionAsync(TranscriptionHistory history)
        {
            await _collection.InsertOneAsync(history);
        }

        public async Task<List<TranscriptionHistory>> GetAllHistoryAsync()
        {
            return await _collection.Find(_ => true).SortByDescending(h => h.CreatedAt).ToListAsync();
        }

        public async Task<bool> DeleteByIdAsync(string id)
        {
            var result = await _collection.DeleteOneAsync(h => h.Id == id);
            return result.DeletedCount > 0;
        }

        public async Task<long> DeleteAllAsync()
        {
            var result = await _collection.DeleteManyAsync(_ => true);
            return result.DeletedCount;
        }
        public async Task<string> CreateProcessingRecordAsync(string fileName, string azureJobUrl)
        {
            var record = new TranscriptionHistory
            {
                FileName = fileName,
                AudioUrl = "",
                AzureJobUrl = azureJobUrl,
                Text = "",
                Status = "Processing",
                CreatedAt = DateTime.UtcNow
            };
            await _collection.InsertOneAsync(record);
            return record.Id!;
        }

        public async Task UpdateResultByAzureJobUrlAsync(string azureJobUrl, string text, string status)
        {
            var filter = Builders<TranscriptionHistory>.Filter.Eq(h => h.AzureJobUrl, azureJobUrl);
            var update = Builders<TranscriptionHistory>.Update
                .Set(h => h.Text, text)
                .Set(h => h.Status, status);
            await _collection.UpdateOneAsync(filter, update);
        }

        public async Task<TranscriptionHistory?> GetByIdAsync(string id)
        {
            return await _collection.Find(h => h.Id == id).FirstOrDefaultAsync();
        }
    }
}