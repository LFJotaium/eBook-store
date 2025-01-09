using ebookStore.Models;
   
   public class BorrowedBook
   {
       public int Id { get; set; }
       public int BookId { get; set; }
       public string Username { get; set; }
       public DateTime BorrowDate { get; set; }
       public DateTime ReturnDate { get; set; }
   
       public Book Book { get; set; }
       public User User { get; set; }
   }