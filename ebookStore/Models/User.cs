using System.ComponentModel.DataAnnotations;

namespace ebookStore.Models;

public class User
{
    [Required] // for name vaildation
    [StringLength(50,MinimumLength = 2, ErrorMessage = "First name must be between 2 and 50 characters")]
    public string FirstName  { get; set; }
    [Required]
    public string LastName  { get; set; }
    [Required] // Email vaildation, 
    [DataType(DataType.EmailAddress)]
    [EmailAddress]
    public string Email  { get; set; }
    [Required]
    [DataType(DataType.Password)]
    public string Password  { get; set; }
    
}