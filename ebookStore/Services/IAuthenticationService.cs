using System.Security.Cryptography;
using System.Text;
using ebookStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ebookStore.Services;

public interface IAuthenticationService
{
    Task<bool> ValidateLoginAsync(string email, string password);
    Task<bool> IsEmailRegisteredAsync(string email);
    Task SaveUserAsync(User user);
}

// Services/AuthenticationService.cs
public class AuthenticationService : IAuthenticationService
{
    private readonly EbookContext _context;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(EbookContext context, ILogger<AuthenticationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> ValidateLoginAsync(string email, string password)
    {
        var hashedPassword = HashPassword(password);
        return await _context.Users.AnyAsync(u => 
            u.Email == email && u.Password == hashedPassword);
    }

    public async Task<bool> IsEmailRegisteredAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email == email);
    }

    public async Task SaveUserAsync(User user)
    {
        user.Password = HashPassword(user.Password);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }
}