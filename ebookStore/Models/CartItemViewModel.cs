namespace ebookStore.Models;
public class CartItemViewModel
{
    public int BookId { get; set; }
    public string Title { get; set; }
    public string AuthorName { get; set; }
    public int Quantity { get; set; }
    public string ActionType { get; set; } // "Buy" or "Borrow"
    public decimal Price;
}