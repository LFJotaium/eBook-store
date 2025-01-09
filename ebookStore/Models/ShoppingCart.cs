using System.ComponentModel.DataAnnotations;

namespace ebookStore.Models
{
    public class ShoppingCart
    {
        public Book book { get; set; }
        [Key]
        public int ID { get; set; }
        public string  Username { get; set; }
        public int BookId { get; set; }
        public int Quantity { get; set; }
        public DateTime CreatedAt { get; set; } // Add this line
        public string ActionType { get; set; }
    }
}
