using AzureTranscription.Api.Models;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AzureTranscription.Api.Services
{
    public interface IMongoService
    {
        Task SaveTranscriptionAsync(TranscriptionHistory history);
        Task<List<TranscriptionHistory>> GetAllHistoryAsync();
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
    }
}