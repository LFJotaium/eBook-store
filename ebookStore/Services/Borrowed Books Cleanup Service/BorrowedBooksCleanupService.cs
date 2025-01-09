using Npgsql;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace ebookStore.Services
{
    public class BorrowedBooksCleanupService
    {
        private readonly string _connectionString;

        public BorrowedBooksCleanupService()
        {
            _connectionString = "Host=localhost;Port=5432;Username=ebookstore_user;Password=ebook;Database=ebookstore;";
        }

        public async Task CleanupOverdueAndNotifyUsersAsync()
        {
            try
            {
                Console.WriteLine("Starting cleanup and notification process...");
                await NotifyUsersAboutUpcomingDueDatesAsync();
                await RemoveOverdueBorrowedBooksAsync();
                Console.WriteLine("Cleanup and notification process completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cleanup task: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw new Exception($"Error during cleanup task: {ex.Message}");
            }
        }

        private async Task RemoveOverdueBorrowedBooksAsync()
        {
            try
            {
                Console.WriteLine("Starting removal of overdue borrowed books...");
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                DateTime currentDateTime = DateTime.Now;
                Console.WriteLine($"Current DateTime: {currentDateTime}");

                var overdueQuery = @"
                    DELETE FROM BorrowedBooks
                    WHERE ReturnDate < @CurrentDateTime AND ReturnDate IS NOT NULL
                    RETURNING BookId, Username";

                var overdueBooks = new List<(int BookId, string Username)>();

                using (var overdueCommand = new NpgsqlCommand(overdueQuery, connection))
                {
                    overdueCommand.Parameters.AddWithValue("@CurrentDateTime", currentDateTime);

                    using (var reader = await overdueCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int bookId = reader.GetInt32(0);
                            string username = reader.GetString(1);
                            overdueBooks.Add((bookId, username));
                            Console.WriteLine($"Found overdue book: BookId={bookId}, Username={username}");
                        }
                    }
                }

                Console.WriteLine($"Total overdue books found: {overdueBooks.Count}");

                foreach (var (bookId, username) in overdueBooks)
                {
                    Console.WriteLine($"Processing overdue book: BookId={bookId}, Username={username}");
                    await IncrementBookCopiesAsync(bookId);
                    await ProcessWaitingListAsync(bookId);
                }

                Console.WriteLine("Finished processing overdue borrowed books.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing overdue borrowed books: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw new Exception($"Error removing overdue borrowed books: {ex.Message}");
            }
        }

        private async Task IncrementBookCopiesAsync(int bookId)
        {
            try
            {
                Console.WriteLine($"Incrementing available copies for BookId={bookId}...");
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                var incrementQuery = "UPDATE Books SET CopiesAvailable = CopiesAvailable + 1 WHERE ID = @BookId";

                using (var incrementCommand = new NpgsqlCommand(incrementQuery, connection))
                {
                    incrementCommand.Parameters.AddWithValue("@BookId", bookId);
                    int rowsAffected = await incrementCommand.ExecuteNonQueryAsync();
                    Console.WriteLine($"Rows affected by incrementing copies: {rowsAffected}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error incrementing book copies for BookId={bookId}: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }

        private async Task ProcessWaitingListAsync(int bookId)
        {
            try
            {
                Console.WriteLine($"Processing waiting list for BookId={bookId}...");
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                var waitingListQuery = @"
                    SELECT Email, Username FROM WaitingList
                    WHERE BookId = @BookId ORDER BY CreatedAt ASC LIMIT 1";

                string firstWaitingUserEmail = null;
                string firstWaitingUserUsername = null;

                using (var waitingCommand = new NpgsqlCommand(waitingListQuery, connection))
                {
                    waitingCommand.Parameters.AddWithValue("@BookId", bookId);

                    using (var reader = await waitingCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            firstWaitingUserEmail = reader.GetString(0);
                            firstWaitingUserUsername = reader.GetString(1);
                            Console.WriteLine($"Found next user in waiting list: Email={firstWaitingUserEmail}, Username={firstWaitingUserUsername}");
                        }
                        else
                        {
                            Console.WriteLine("No users found in the waiting list for this book.");
                        }
                    }
                }

                if (!string.IsNullOrEmpty(firstWaitingUserEmail))
                {
                    Console.WriteLine($"Sending email to waiting list user: {firstWaitingUserEmail}");
                    await SendEmailForAvailableBook(firstWaitingUserEmail, bookId);

                    var addToCartQuery = "INSERT INTO ShoppingCart (BookId, Username, ActionType) VALUES (@BookId, @Username, 'Borrow')";

                    using (var addToCartCommand = new NpgsqlCommand(addToCartQuery, connection))
                    {
                        addToCartCommand.Parameters.AddWithValue("@BookId", bookId);
                        addToCartCommand.Parameters.AddWithValue("@Username", firstWaitingUserUsername);
                        int rowsAffected = await addToCartCommand.ExecuteNonQueryAsync();
                        Console.WriteLine($"Rows affected by adding to cart: {rowsAffected}");
                    }

                    var removeFromWaitingListQuery = "DELETE FROM WaitingList WHERE BookId = @BookId AND Username = @Username";

                    using (var removeFromWaitingListCommand = new NpgsqlCommand(removeFromWaitingListQuery, connection))
                    {
                        removeFromWaitingListCommand.Parameters.AddWithValue("@BookId", bookId);
                        removeFromWaitingListCommand.Parameters.AddWithValue("@Username", firstWaitingUserUsername);
                        int rowsAffected = await removeFromWaitingListCommand.ExecuteNonQueryAsync();
                        Console.WriteLine($"Rows affected by removing from waiting list: {rowsAffected}");
                    }

                    var decreaseCopiesQuery = "UPDATE Books SET CopiesAvailable = CopiesAvailable - 1 WHERE ID = @BookId";

                    using (var decreaseCommand = new NpgsqlCommand(decreaseCopiesQuery, connection))
                    {
                        decreaseCommand.Parameters.AddWithValue("@BookId", bookId);
                        int rowsAffected = await decreaseCommand.ExecuteNonQueryAsync();
                        Console.WriteLine($"Rows affected by decreasing copies: {rowsAffected}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing waiting list for BookId={bookId}: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }

        private async Task NotifyUsersAboutUpcomingDueDatesAsync()
        {
            try
            {
                Console.WriteLine("Starting notification process for upcoming due dates...");
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                DateTime fiveDaysFromNow = DateTime.Now.AddDays(5);
                Console.WriteLine($"Notifying users with due dates on: {fiveDaysFromNow}");

                var query = @"
                    SELECT Username, BookId, ReturnDate 
                    FROM BorrowedBooks 
                    WHERE ReturnDate = @FiveDaysFromNow";

                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@FiveDaysFromNow", fiveDaysFromNow);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string username = reader.GetString(0);
                            int bookId = reader.GetInt32(1);
                            DateTime returnDate = reader.GetDateTime(2);

                            Console.WriteLine($"Found user with upcoming due date: Username={username}, BookId={bookId}, ReturnDate={returnDate}");

                            string userEmail = await GetUserEmailAsync(username);

                            if (!string.IsNullOrEmpty(userEmail))
                            {
                                Console.WriteLine($"Sending email to user: {userEmail}");
                                await SendEmailForUpcomingDueDate(userEmail, bookId, returnDate);
                            }
                            else
                            {
                                Console.WriteLine($"No email found for user: {username}");
                            }
                        }
                    }
                }

                Console.WriteLine("Finished notifying users about upcoming due dates.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error notifying users about upcoming due dates: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw new Exception($"Error notifying users about upcoming due dates: {ex.Message}");
            }
        }

        private async Task<string> GetUserEmailAsync(string username)
        {
            try
            {
                Console.WriteLine($"Fetching email for user: {username}");
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = "SELECT Email FROM Users WHERE Username = @Username";
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Username", username);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            string email = reader.GetString(0);
                            Console.WriteLine($"Found email for user {username}: {email}");
                            return email;
                        }
                    }
                }

                Console.WriteLine($"No email found for user: {username}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching email for user {username}: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }

        private async Task SendEmailForAvailableBook(string email, int bookId)
        {
            try
            {
                Console.WriteLine($"Sending email to {email} for available book: BookId={bookId}");
                string subject = "A Book You Requested is Now Available!";
                string body = $@"
                    <html>
                    <body>
                        <h2>Good news!</h2>
                        <p>The book you requested (ID: {bookId}) is now available. Please log in to your account to borrow it.</p>
                    </body>
                    </html>";

                await SendEmailAsync(email, subject, body);
                Console.WriteLine($"Email sent successfully to {email}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending email to {email}: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }

        private async Task SendEmailForUpcomingDueDate(string email, int bookId, DateTime returnDate)
        {
            try
            {
                Console.WriteLine($"Sending email to {email} for upcoming due date: BookId={bookId}, ReturnDate={returnDate}");
                string subject = "Upcoming Due Date for Borrowed Book";
                string body = $@"
                    <html>
                    <body>
                        <h2>Reminder!</h2>
                        <p>The book you borrowed (ID: {bookId}) is due on {returnDate:MMMM dd, yyyy}. Please return it on time to avoid penalties.</p>
                    </body>
                    </html>";

                await SendEmailAsync(email, subject, body);
                Console.WriteLine($"Email sent successfully to {email}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending email to {email}: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                Console.WriteLine($"Preparing to send email to {toEmail}...");
                string fromMail = "malikabushah@gmail.com";
                string fromPassword = "vsjm dvly keqg ymzl";

                var message = new MailMessage
                {
                    From = new MailAddress(fromMail),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                message.To.Add(new MailAddress(toEmail));

                using var smtpClient = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(fromMail, fromPassword),
                    EnableSsl = true
                };

                await smtpClient.SendMailAsync(message);
                Console.WriteLine($"Email sent successfully to {toEmail}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending email to {toEmail}: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}