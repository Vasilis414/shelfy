namespace Shelfy.Models;

public class FineRule
{
    public int RuleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public float Fee { get; set; }
    public int FeeInterval { get; set; }
}