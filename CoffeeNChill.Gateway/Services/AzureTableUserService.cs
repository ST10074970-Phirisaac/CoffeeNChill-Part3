
using Azure;
using Azure.Data.Tables;
using CoffeeNChill.Gateway.Models;

namespace CoffeeNChill.Gateway.Services;

/// <summary>
/// Stores and retrieves CoffeeNChill users in Azure Table Storage.
/// </summary>
public class AzureTableUserService : IUserService
{
    private readonly TableClient _tableClient;
    private readonly TableClient _emailTableClient;

    public AzureTableUserService(IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("AzureStorage")
            ?? throw new InvalidOperationException(
                "Azure Storage connection string is missing.");

        var tableServiceClient =
            new TableServiceClient(connectionString);

        _tableClient =
            tableServiceClient.GetTableClient("Users");

        _emailTableClient =
            tableServiceClient.GetTableClient("UserEmails");
    }

    public async Task<UserEntity?> GetByEmailAsync(string email)
    {
        await _tableClient.CreateIfNotExistsAsync();

        var normalizedEmail = email.Trim().ToLowerInvariant();

        await foreach (var user in _tableClient.QueryAsync<UserEntity>(
            user => user.RowKey == normalizedEmail))
        {
            return user;
        }

        return null;
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await GetByEmailAsync(email) != null;
    }

    public async Task CreateUserAsync(UserEntity user)
    {
        await _tableClient.CreateIfNotExistsAsync();
        await _emailTableClient.CreateIfNotExistsAsync();

        var normalizedEmail =
            user.RowKey.Trim().ToLowerInvariant();

        user.RowKey = normalizedEmail;

        // Protect existing users created before the email index.
        if (await EmailExistsAsync(normalizedEmail))
        {
            throw new RequestFailedException(
                409, "Email address is already registered.");
        }

        var emailReservation = new TableEntity(
            "Email", normalizedEmail)
        {
            { "Role", user.PartitionKey },
            { "CreatedDate", DateTimeOffset.UtcNow }
        };

        // Atomic insert: only one request can reserve this email.
        await _emailTableClient.AddEntityAsync(emailReservation);

        try
        {
            // Recheck existing users after claiming the email.
            if (await EmailExistsAsync(normalizedEmail))
            {
                throw new RequestFailedException(
                    409, "Email address is already registered.");
            }

            await _tableClient.AddEntityAsync(user);
        }
        catch
        {
            // Release the reservation if user creation fails.
            try
            {
                await _emailTableClient.DeleteEntityAsync(
                    "Email", normalizedEmail);
            }
            catch (RequestFailedException ex)
                when (ex.Status == 404)
            {
                // Reservation was already removed.
            }

            throw;
        }
    }
}
