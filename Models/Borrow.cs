namespace Shelfy.Models;

public class Borrow
{
    public int BorrowId { get; set; }
    public int UserId { get; set; }
    public int BookId { get; set; }
    public int Status { get; set; }
    public decimal Fine { get; set; }
    public int BorrowRule { get; set; }
    public DateTime BorrowedAt { get; set; } = DateTime.Now;
    public DateTime? BorrowReturnDate { get; set; }
    public DateTime? ActualReturn { get; set; }
    public DateTime? FineCalculatedUntil { get; set; }

    public string StatusText => Status switch
    {
        0 => "Active",
        1 => "Returned",
        2 => "Overdue",
        3 => "Late Return",
        _ => "Unknown"
    };
    
    public User? User { get; set; }
    public Book? Book { get; set; }
}
