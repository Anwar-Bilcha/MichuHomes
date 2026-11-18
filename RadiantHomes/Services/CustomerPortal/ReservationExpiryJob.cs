namespace RadiantHomes.Services.CustomerPortal
{
    /// <summary>
    /// Every few minutes, expires pending reservations whose answer time has passed (ReservationService.ExpireDueAsync):
    /// the home is listed again and the customer and company are notified. The answer time is a platform setting.
    /// </summary>
    public sealed class ReservationExpiryJob : BackgroundService
    {
        public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<ReservationExpiryJob> _logger;

        public ReservationExpiryJob(IServiceScopeFactory scopes, ILogger<ReservationExpiryJob> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // let start-up finish first
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        using var scope = _scopes.CreateScope();
                        var expired = await scope.ServiceProvider.GetRequiredService<ReservationService>().ExpireDueAsync();
                        if (expired > 0) _logger.LogInformation("Reservations: {Count} pending reservation(s) expired.", expired);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Reservations: expiring overdue reservations failed; trying again in {Minutes} minutes.", Interval.TotalMinutes);
                    }
                    await Task.Delay(Interval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // the app is stopping
            }
        }
    }
}
