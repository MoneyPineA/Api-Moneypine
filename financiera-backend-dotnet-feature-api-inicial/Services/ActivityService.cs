using ApiEjemplo.Data;
using ApiEjemplo.Enums;
using ApiEjemplo.Models;

namespace ApiEjemplo.Services
{
    public class ActivityService
    {
        private readonly AppDbContext _context;

        public ActivityService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ActivityLog> CreateActivity(
            ActivityType type,
            int clientId,
            decimal amount,
            NotificationLevel priority
        )
        {
            var activity = new ActivityLog
            {
                Type = type,
                ClientId = clientId,
                Amount = amount,
                Priority = priority
            };

            _context.ActivityLogs.Add(activity);
            await _context.SaveChangesAsync();

            return activity;
        }
    }
}