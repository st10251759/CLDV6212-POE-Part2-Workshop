using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PageTurn.Functions.Helpers;
using PageTurn.Functions.Services;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

// One connection string for everything. Locally this is Azurite ("UseDevelopmentStorage=true").
string connectionString = builder.Configuration[StorageNames.ConnectionSetting] ?? "UseDevelopmentStorage=true";

// Storage clients are thread-safe and registered as singletons.
builder.Services.AddSingleton(new TableServiceClient(connectionString));
builder.Services.AddSingleton(new BlobServiceClient(connectionString));

// Part 2: Base64 so the QueueTrigger can decode messages produced by the HTTP endpoint.
builder.Services.AddSingleton(new QueueServiceClient(connectionString,
    new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 }));

// Part 1 services
builder.Services.AddSingleton<IBookService, BookService>();
builder.Services.AddSingleton<IDocumentService, DocumentService>();

// Part 2 services
builder.Services.AddSingleton<IOrderQueueService, OrderQueueService>();
builder.Services.AddSingleton<IOrderService, OrderService>();

builder.Build().Run();