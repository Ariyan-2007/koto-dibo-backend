using KotoDibo.Api.Extensions;
using KotoDibo.Application;
using KotoDibo.Infrastructure.Extensions;
using KotoDibo.Infrastructure.Persistence.MongoDb;
using KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration, builder.Environment);

var app = builder.Build();

await MongoIndexInitializer.InitializeAsync(app.Services.GetRequiredService<MongoDbContext>());

app.UseApiPipeline();

app.Run();

public partial class Program;
