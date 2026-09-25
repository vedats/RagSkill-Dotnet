using Microsoft.Extensions.AI;
using OllamaSharp;
using RagChatTemplate.Components;
using RagChatTemplate.Services;
using RagChatTemplate.Services.Ingestion;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Chat model (must support tool calling). Names ending in "-cloud" run on Ollama Cloud through the local
// Ollama app, which must be signed in; other names must be pulled locally first.
IChatClient chatClient = new OllamaApiClient(new Uri("http://localhost:11434"),
    "gpt-oss:120b-cloud");
// Ollama Cloud has no embedding models, so embeddings always run locally ("ollama pull" the model first).
// If you change this model, update IngestedChunk.VectorDimensions and delete vector-store.db + its manifest.
IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = new OllamaApiClient(new Uri("http://localhost:11434"),
    "all-minilm");

var vectorStorePath = Path.Combine(AppContext.BaseDirectory, "vector-store.db");
var vectorStoreConnectionString = $"Data Source={vectorStorePath}";
builder.Services.AddSqliteVectorStore(_ => vectorStoreConnectionString);
builder.Services.AddSqliteCollection<Guid, IngestedChunk>(IngestedChunk.CollectionName, vectorStoreConnectionString);

builder.Services.AddSingleton<DataIngestor>();
builder.Services.AddSingleton<SemanticSearch>();
builder.Services.AddSingleton(services => new ChatHistoryStore(
    Path.Combine(AppContext.BaseDirectory, "conversations"), services.GetRequiredService<ILogger<ChatHistoryStore>>()));
builder.Services.AddHostedService<DocumentSyncService>();
builder.Services.AddKeyedSingleton("ingestion_directory", new DirectoryInfo(Path.Combine(builder.Environment.WebRootPath, "Data")));
// Records which file versions are already indexed, so unchanged documents aren't re-indexed on every start
builder.Services.AddKeyedSingleton("ingestion_manifest", new FileInfo(Path.ChangeExtension(vectorStorePath, ".manifest.json")));
builder.Services.AddChatClient(chatClient).UseFunctionInvocation().UseLogging();
builder.Services.AddEmbeddingGenerator(embeddingGenerator)
    .Use((inner, services) => new RetryingEmbeddingGenerator(inner, services.GetRequiredService<ILogger<RetryingEmbeddingGenerator>>()));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.UseStaticFiles();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
