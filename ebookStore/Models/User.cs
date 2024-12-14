using System.ComponentModel.DataAnnotations;
using Npgsql;

namespace ebookStore.Models
{

    public class User
    {
        [Required] // for name vaildation
        [StringLength(50, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 50 characters")]
        public string FirstName { get; set; }

        [StringLength(50, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 50 characters")]
        public string LastName { get; set; }

        [Key] // primary key 
        public string Username { get; set; }

        [Required] // Email vaildation, 
        [DataType(DataType.EmailAddress)]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d]{8,}$",
            ErrorMessage =
                "Password must be at least 8 characters long and contain at least one letter and one number.")]
        public string Password { get; set; }

        [Required] public string Role { get; set; } = "User"; // Default role is "User"
        
    }
}