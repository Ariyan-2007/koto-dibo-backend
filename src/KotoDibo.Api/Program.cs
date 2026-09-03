using KotoDibo.Api.Extensions;
using KotoDibo.Application;
using KotoDibo.Infrastructure.Extensions;
using KotoDibo.Infrastructure.Persistence.MongoDb;
using KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;
using KotoDibo.Infrastructure.Persistence.MongoDb.Migrations;
using KotoDibo.Infrastructure.Persistence.MongoDb.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration, builder.Environment);

var app = builder.Build();

var mongoDbContext = app.Services.GetRequiredService<MongoDbContext>();
await MongoIndexInitializer.InitializeAsync(mongoDbContext);
await TariffConfigSeeder.SeedAsync(mongoDbContext);
await ExpenseCategorySeeder.SeedAsync(mongoDbContext);
await BazarContributionMirrorBackfill.RunAsync(mongoDbContext);

app.UseApiPipeline();

app.Run();

public partial class Program;
