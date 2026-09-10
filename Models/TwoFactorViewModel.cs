using System.ComponentModel.DataAnnotations;

namespace Shelfy.Models;

public class TwoFactorViewModel
{
    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
}
