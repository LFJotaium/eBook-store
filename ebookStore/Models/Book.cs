using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
    public decimal? PriceBorrowing {get;set;}
    [Required]
    public int YearOfPublish {get;set;}
    public string Genre {get;set;}
    public string CoverImagePath {get;set;}
    //price related 
    public Price Price { get; set; }
    public int? CopiesAvailable { get; set; }
    public int SoldCopies { get; set; }
    public int BorrowedCopies {  get; set; }
    public int? AgeLimit { get; set; }
    public string Files {get;set;}
    public bool IsPopular { get; set; }
    // Public property for the view
    public bool IsBuyOnly { get; set; }
    // Internal property for database interaction
    [NotMapped] // Tell EF Core to ignore this property
    /*public int IsBuyOnlyDb
    {
        get => IsBuyOnly ? 1 : 0;
        set => IsBuyOnly = value == 1;
    }*/
    public virtual ICollection<BookFeedback> Feedbacks { get; set; } = new List<BookFeedback>();
    public class BookFeedback
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BookId { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
        public int Rating { get; set; }

        [MaxLength(500)]
        public string Comment { get; set; }
    }
}