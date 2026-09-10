namespace Shelfy.Models;

public class AccountInfoViewModel
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Admin { get; set; }
    public bool TwoFactorAuth { get; set; }
    public decimal TotalFines {get;set;} = 0;

    public string Role => Admin switch
    {
        2 => "Super Admin",
        1 => "Admin",
        _ => "Member"
    };
}
