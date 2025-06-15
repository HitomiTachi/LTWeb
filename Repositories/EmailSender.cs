using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.Tasks;

public class EmailSender : IEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        // Tạm thời chỉ log ra Console để kiểm tra
        Console.WriteLine($"[Email Gửi Đến] {email}\n[Chủ đề] {subject}\n[Nội dung] {htmlMessage}");
        return Task.CompletedTask;
    }
}
