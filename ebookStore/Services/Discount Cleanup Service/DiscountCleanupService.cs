 using Npgsql;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ebookStore.Services
{
    public class DiscountCleanupService : BackgroundService
    {
        private readonly string _connectionString;

        public DiscountCleanupService()
        {
            _connectionString = "Host=localhost;Port=5432;Username=ebookstore_user;Password=ebook;Database=ebookstore;";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Console.WriteLine("Checking for expired discounts...");
                    await RevertExpiredDiscountsAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in DiscountCleanupService: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                }

                // Wait for a specific interval before running again (e.g., every hour)
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        public async Task RevertExpiredDiscountsAsync()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            DateTime currentDateTime = DateTime.Now;

            var query = @"
                UPDATE Prices
                SET IsDiscounted = @IsDiscounted,
                    CurrentPriceBuy = OriginalPriceBuy,
                    CurrentPriceBorrow = OriginalPriceBorrow,
                    DiscountEndDate = NULL
                WHERE DiscountEndDate < @CurrentDateTime AND IsDiscounted = @IsDiscountedTrue";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@IsDiscounted", false);
            command.Parameters.AddWithValue("@IsDiscountedTrue", true);
            command.Parameters.AddWithValue("@CurrentDateTime", currentDateTime);

            int rowsAffected = await command.ExecuteNonQueryAsync();
            Console.WriteLine($"Reverted {rowsAffected} expired discounts.");
        }
    }
}