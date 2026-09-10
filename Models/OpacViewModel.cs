using System.Collections.Generic;

namespace Shelfy.Models
{
    public class OpacViewModel
    {
        public List<Book> Books { get; set; } = new List<Book>();
        public List<Borrow> BorrowHistory { get; set; } = new List<Borrow>();
    }
}
