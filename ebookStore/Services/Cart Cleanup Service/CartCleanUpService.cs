using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ebookStore.Services
{
    public class CartCleanupService
    {
        private readonly string _connectionString;

        public CartCleanupService()
        {
            _connectionString = "Host=localhost;Port=5432;Username=ebookstore_user;Password=ebook;Database=ebookstore;";
        }

        public async Task RemoveExpiredCartItemsAsync()
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                DateTime currentDateTime = DateTime.Now;

                var expiredQuery = @"
                    DELETE FROM ShoppingCart
                    WHERE CreatedAt < @CurrentDateTime
                    RETURNING BookId, Username";

                var expiredCartItems = new List<(int BookId, string Username)>();

                using (var expiredCommand = new NpgsqlCommand(expiredQuery, connection))
                {
                    expiredCommand.Parameters.AddWithValue("@CurrentDateTime", currentDateTime.AddMinutes(-1));

                    using (var reader = await expiredCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int bookId = reader.GetInt32(0);
                            string username = reader.GetString(1);
                            expiredCartItems.Add((bookId, username));
                        }
                    }
                }

                foreach (var (bookId, username) in expiredCartItems)
                {
                    await IncrementBookCopiesAsync(bookId);
                    await ProcessWaitingListAsync(bookId);
                }

                Console.WriteLine($"Removed {expiredCartItems.Count} expired items from the cart.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing expired cart items: {ex.Message}");
            }
        }

        private async Task IncrementBookCopiesAsync(int bookId)
        {
            Console.WriteLine("Incrementing Book ID " + bookId);

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var incrementQuery = "UPDATE Books SET CopiesAvailable = CopiesAvailable + 1 WHERE ID = @BookId";

            using (var incrementCommand = new NpgsqlCommand(incrementQuery, connection))
            {
                incrementCommand.Parameters.AddWithValue("@BookId", bookId);
                await incrementCommand.ExecuteNonQueryAsync();
            }
        }

        private async Task ProcessWaitingListAsync(int bookId)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var waitingListQuery = @"
                SELECT Username FROM WaitingList
                WHERE BookId = @BookId ORDER BY CreatedAt ASC LIMIT 1";

            string firstWaitingUserUsername = null;

            using (var waitingCommand = new NpgsqlCommand(waitingListQuery, connection))
            {
                waitingCommand.Parameters.AddWithValue("@BookId", bookId);

                using (var reader = await waitingCommand.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        firstWaitingUserUsername = reader.GetString(0);
                    }
                }
            }

            if (!string.IsNullOrEmpty(firstWaitingUserUsername))
            {
                var addToCartQuery = "INSERT INTO ShoppingCart (BookId, Username, ActionType) VALUES (@BookId, @Username, 'Borrow')";

                using (var addToCartCommand = new NpgsqlCommand(addToCartQuery, connection))
                {
                    addToCartCommand.Parameters.AddWithValue("@BookId", bookId);
                    addToCartCommand.Parameters.AddWithValue("@Username", firstWaitingUserUsername);
                    await addToCartCommand.ExecuteNonQueryAsync();
                }

                var removeFromWaitingListQuery = "DELETE FROM WaitingList WHERE BookId = @BookId AND Username = @Username";

                using (var removeFromWaitingListCommand = new NpgsqlCommand(removeFromWaitingListQuery, connection))
                {
                    removeFromWaitingListCommand.Parameters.AddWithValue("@BookId", bookId);
                    removeFromWaitingListCommand.Parameters.AddWithValue("@Username", firstWaitingUserUsername);
                    await removeFromWaitingListCommand.ExecuteNonQueryAsync();
                }

                var decreaseCopiesQuery = "UPDATE Books SET CopiesAvailable = CopiesAvailable - 1 WHERE ID = @BookId";

                using (var decreaseCommand = new NpgsqlCommand(decreaseCopiesQuery, connection))
                {
                    decreaseCommand.Parameters.AddWithValue("@BookId", bookId);
                    await decreaseCommand.ExecuteNonQueryAsync();
                }
            }
        }
    }
}