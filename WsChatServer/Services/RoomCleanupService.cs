namespace WsChatServer.Services;

public class RoomCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly RoomManager _roomManager;
    private readonly ILogger<RoomCleanupService> _logger;

    public RoomCleanupService(RoomManager roomManager, ILogger<RoomCleanupService> logger)
    {
        _roomManager = roomManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RoomCleanupService started. Interval: {Interval}.", Interval);

        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var removed = _roomManager.CleanupEmptyRooms();

            if (removed.Count > 0)
                _logger.LogInformation(
                    "Cleanup: removed {Count} empty room(s): {Ids}",
                    removed.Count,
                    string.Join(", ", removed));
            else
                _logger.LogDebug("Cleanup: no empty rooms found.");
        }
    }
}
