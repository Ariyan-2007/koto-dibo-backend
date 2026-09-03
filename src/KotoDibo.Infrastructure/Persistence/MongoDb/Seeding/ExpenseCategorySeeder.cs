using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Seeding;

// Seeds the system-default expense category tree (UserId == null, shared by every user) every
// user's category picker starts populated with, instead of hardcoding a category vocabulary into
// ExpenseService/BudgetService business logic. Deliberately idempotent — only inserts when no
// system-default categories exist yet, so it never clobbers categories an operator has since
// edited/deactivated.
public static class ExpenseCategorySeeder
{
    public static async Task SeedAsync(MongoDbContext context, CancellationToken cancellationToken = default)
    {
        var collection = context.GetCollection<ExpenseCategory>(nameof(ExpenseCategory));

        var alreadySeeded = await collection.Find(c => c.IsSystemDefault).AnyAsync(cancellationToken);
        if (alreadySeeded)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var categories = new List<ExpenseCategory>();

        // Ids are assigned client-side (before InsertManyAsync) so children can reference their
        // parent's Id as ParentCategoryId within the same batch insert.
        ExpenseCategory AddTopLevel(string name, string icon)
        {
            var category = new ExpenseCategory
            {
                Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
                Name = name,
                Icon = icon,
                IsSystemDefault = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            };
            categories.Add(category);
            return category;
        }

        void AddChild(ExpenseCategory parent, string name)
        {
            categories.Add(new ExpenseCategory
            {
                Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
                ParentCategoryId = parent.Id,
                Name = name,
                IsSystemDefault = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        var food = AddTopLevel("Food", "utensils");
        AddChild(food, "Groceries");
        AddChild(food, "Restaurants");
        AddChild(food, "Fast Food");
        AddChild(food, "Coffee");

        var transport = AddTopLevel("Transportation", "car");
        AddChild(transport, "Fuel");
        AddChild(transport, "Ride Sharing");
        AddChild(transport, "Public Transport");
        AddChild(transport, "Maintenance");

        AddTopLevel("Housing", "home");
        AddTopLevel("Utilities", "bolt");
        AddTopLevel("Healthcare", "heart-pulse");
        AddTopLevel("Education", "graduation-cap");
        AddTopLevel("Entertainment", "film");
        AddTopLevel("Shopping", "shopping-bag");
        AddTopLevel("Travel", "plane");
        AddTopLevel("Subscriptions", "repeat");
        AddTopLevel("Personal Care", "sparkles");
        AddTopLevel("Insurance", "shield");
        AddTopLevel("Debt Payments", "credit-card");
        AddTopLevel("Family", "users");
        AddTopLevel("Other", "ellipsis");

        await collection.InsertManyAsync(categories, options: null, cancellationToken);
    }
}
