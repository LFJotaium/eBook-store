namespace ebookStore.Models;
public class Price
{
    public int ID { get; set; }
    public int BookID { get; set; }
    public decimal CurrentPriceBuy { get; set; }
    public decimal CurrentPriceBorrow { get; set; }
    public decimal OriginalPriceBuy { get; set; }
    public decimal OriginalPriceBorrow { get; set; }
    public bool IsDiscounted { get; set; }
    public DateTime? DiscountEndDate { get; set; }
    
    //public Book Book { get; set; }
}