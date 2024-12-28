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
   public string Publshier {get;set;}
   [Required]
   public decimal PriceBuy {get;set;}
   [Required]
   public decimal PriceBorrowing {get;set;}
   [Required]
   public int YearOfPublish {get;set;}
   public string Genre {get;set;}
   public string CoverImagePath {get;set;}
}