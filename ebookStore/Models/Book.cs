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
    public int CopiesAvailable { get; set; }
    public int SoldCopies { get; set; }
    public int BorrowedCopies {  get; set; }
    public int AgeLimit { get; set; }
    public string DrivePathFiles {get;set;}
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