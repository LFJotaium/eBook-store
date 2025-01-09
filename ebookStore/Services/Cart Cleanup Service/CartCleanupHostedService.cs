using ebookStore.Services;

public class CartCleanupHostedService : IHostedService, IDisposable
{
    private readonly CartCleanupService _cartCleanupService;
    private Timer _timer;

    public CartCleanupHostedService(CartCleanupService cartCleanupService)
    {
        _cartCleanupService = cartCleanupService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(ExecuteCleanup, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        return Task.CompletedTask;
    }

    private async void ExecuteCleanup(object state)
    {
        try
        {
            await _cartCleanupService.RemoveExpiredCartItemsAsync();
            Console.WriteLine($"Cart cleanup executed at {DateTime.Now}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing cleanup: {ex.Message}");
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