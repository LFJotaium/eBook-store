using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using ebookStore.Services;

namespace ebookStore.BackgroundServices
{
    public class BorrowedBooksCleanupHostedService : IHostedService, IDisposable
    {
        private readonly BorrowedBooksCleanupService _cleanupService;
        private Timer _timer;

        public BorrowedBooksCleanupHostedService(BorrowedBooksCleanupService cleanupService)
        {
            _cleanupService = cleanupService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(ExecuteCleanupTask, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
            return Task.CompletedTask;
        }

        private async void ExecuteCleanupTask(object state)
        {
            try
            {
                await _cleanupService.CleanupOverdueAndNotifyUsersAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing cleanup task: {ex.Message}");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}