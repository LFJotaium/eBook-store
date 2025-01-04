using System.ComponentModel.DataAnnotations;

namespace ebookStore.Models
{
    public class PurchasedBook
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public string Username { get; set; }
        public DateTime PurchaseDate { get; set; }

        public Book Book { get; set; }
        public User User { get; set; }
    }
}