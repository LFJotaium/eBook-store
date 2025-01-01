using System.ComponentModel.DataAnnotations;

namespace ebookStore.Models;

public class Book
{
   [Key]
   public int ID {get;set;}
   [Required]
   public  string Title {get;set;}
   [Required]
   public string AuthorName {get;set;}
   [Required]
   public string Publisher {get;set;}
   [Required]
   public decimal PriceBuy {get;set;}
   [Required]
   public decimal PriceBorrowing {get;set;}
   [Required]
   public int YearOfPublish {get;set;}
   public string Genre {get;set;}
   public string CoverImagePath {get;set;}
   //price related 
   public Price Price { get; set; }
   /*
   public bool IsBorrowable { get; set; }
   public int AvailableCopies { get; set; }
   public List<string> Formats { get; set; }
   public int AgeRestriction { get; set; }
*/
}