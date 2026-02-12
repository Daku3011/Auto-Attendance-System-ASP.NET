using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace DemoAAS.Hubs
{
    public class AttendanceHub : Hub
    {
        public async Task SendAttendanceUpdate(string studentName, string status)
        {
            await Clients.All.SendAsync("ReceiveAttendanceUpdate", studentName, status);
        }
    }
}
