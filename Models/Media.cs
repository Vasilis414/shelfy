namespace Shelfy.Models;

public class Media
{
    public int MediaId { get; set; }
    public int BookId { get; set; }
    public string? Path { get; set; }
    
    public Book? Book { get; set; }
}
