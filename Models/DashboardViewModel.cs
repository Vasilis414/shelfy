using System.Collections.Generic;

namespace Shelfy.Models
{
    public class DashboardViewModel
    {
        public int TotalBooks { get; set; }
        public int TotalMembers { get; set; }
        public int TotalBorrows { get; set; }
        public int ActiveBorrows { get; set; }
        public int OverdueBorrows { get; set; }
        public List<Borrow> OverdueBorrowRecords { get; set; } = new List<Borrow>();
    }
}
