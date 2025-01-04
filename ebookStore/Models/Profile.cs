namespace ebookStore.Models.ViewModels
{
    public class ProfileViewModel
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public List<BorrowedBookViewModel> BorrowedBooks { get; set; } = new List<BorrowedBookViewModel>();
        public List<PurchasedBookViewModel> PurchasedBooks { get; set; } = new List<PurchasedBookViewModel>();
    }

    public class BorrowedBookViewModel
    {
        public string Title { get; set; }
        public string Author { get; set; }
       
        public int BookId { get; set; }

        public DateTime BorrowDate { get; set; }
        public DateTime ReturnDate { get; set; }
    }

    public class PurchasedBookViewModel
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public int BookId { get; set; }

        public DateTime PurchaseDate { get; set; }
    }
}