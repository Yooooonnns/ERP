using DigitalisationERP.Infrastructure.Identity.Data;

namespace DigitalisationERP.API.Services;

/// <summary>
/// Interface for seeding initial data into the database.
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// Seeds the database with initial data if it's empty.
    /// </summary>
    /// <param name="context">The application database context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SeedAsync(ApplicationDbContext context);
}
