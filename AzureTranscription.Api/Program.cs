using System.Reflection;
using AzureTranscription.Api.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using AzureTranscription.Api.Configuration;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AzureSpeechServicesOptions>(
    builder.Configuration.GetSection("AzureSpeechServices"));

// 1. Add essential framework services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    options.IncludeXmlComments(xmlPath);
});

// 2. Add Lana's File Processing & Validation Registrations
builder.Services.AddScoped<IFileValidationService, FileValidationService>();

// 2b. Add Sona's Transcription Service (Azure Blob + Speech integration)
builder.Services.AddScoped<ITranscriptionService, TranscriptionService>();
builder.Services.AddHttpClient<TranscriptionService>();

// CRITICAL KESTREL SERVER LIMIT OVERRIDE TO STOP THE EXE CRASH
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 60 * 1024 * 1024; // Server limit set to 60 MB
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 60 * 1024 * 1024; // Form limit set to 60 MB
    options.MemoryBufferThreshold = int.MaxValue;
});

// 3. Configure CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 4. Build the application
var app = builder.Build();

// 5. Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Run();