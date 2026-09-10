namespace Shelfy.Models;

public class User
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int Admin { get; set; } = 0;
    public bool TwoFactorAuth { get; set; }

    public ICollection<Borrow> Borrows { get; set; } = new List<Borrow>();
}