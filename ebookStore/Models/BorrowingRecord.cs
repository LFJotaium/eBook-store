namespace ebookStore.Models;

public class BorrowingRecord
{
    public int BookId { get; set; }
    public string Title { get; set; }
    public string Username { get; set; }
    public DateTime BorrowDate { get; set; }
    public DateTime ReturnDate { get; set; }
    public string Status { get; set; } // e.g., Active or Overdue
}
