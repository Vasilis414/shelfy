namespace Shelfy.Models;

public class Book
{
    public int BookId { get; set; }
    public string Isbn { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int Available { get; set; }
    public int BookFineRule { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }

    public ICollection<Borrow> Borrows { get; set; } = new List<Borrow>();
    public ICollection<Media> Media { get; set; } = new List<Media>();
}
