using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Services.Background
{
    public class SessionStatusUpdater : BackgroundService
    {
        // Dùng IServiceScopeFactory vì DbContext không phải singleton
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SessionStatusUpdater> _logger;

        public SessionStatusUpdater(
            IServiceScopeFactory scopeFactory,
            ILogger<SessionStatusUpdater> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SessionStatusUpdater đã khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await UpdateSessionStatuses();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi cập nhật trạng thái Session.");
                }

                // Chạy lại sau mỗi 1 phút
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task UpdateSessionStatuses()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider
                                     .GetRequiredService<ApplicationDbContext>();

            var today = DateOnly.FromDateTime(DateTime.Now);
            var currentTime = TimeOnly.FromDateTime(DateTime.Now);

            // Lấy tất cả session hôm nay chưa bị Cancelled
            var todaySessions = await context.Sessions
                .Include(s => s.Shift)
                .Where(s => s.SessionDate == today
                         && s.Status != SessionStatus.Cancelled)
                .ToListAsync();

            if (!todaySessions.Any()) return;

            int updatedCount = 0;

            foreach (var session in todaySessions)
            {
                var newStatus = TinhTrangThaiMoi(session, currentTime);

                if (newStatus.HasValue && session.Status != newStatus.Value)
                {
                    session.Status = newStatus.Value;
                    updatedCount++;
                }
            }

            if (updatedCount > 0)
            {
                await context.SaveChangesAsync();
                _logger.LogInformation(
                    "Đã cập nhật {Count} session lúc {Time}.",
                    updatedCount, DateTime.Now.ToString("HH:mm:ss"));
            }
        }

        private static SessionStatus? TinhTrangThaiMoi(
            Data.Entities.Session session, TimeOnly currentTime)
        {
            var startTime = session.Shift.StartTime;
            var endTime = session.Shift.EndTime;

            // Giờ hiện tại > EndTime → Completed
            if (currentTime > endTime)
                return SessionStatus.Completed;

            // Giờ hiện tại >= StartTime và <= EndTime → Ongoing
            if (currentTime >= startTime && currentTime <= endTime)
                return SessionStatus.Ongoing;

            // Chưa đến giờ → giữ nguyên Scheduled
            return null;
        }
    }
}