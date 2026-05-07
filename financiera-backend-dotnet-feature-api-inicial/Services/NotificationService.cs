using ApiEjemplo.Data;
using ApiEjemplo.Enums;
using ApiEjemplo.Models;

namespace ApiEjemplo.Services
{
    public class NotificationService
    {
        private readonly AppDbContext _context;

        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Notification> CreateNotification(
            int userId,
            string message,
            NotificationLevel level
        )
        {
            var notification = new Notification
            {
                UsuarioId = userId,
                Message = message,
                Level = level
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return notification;
        }
    }
}