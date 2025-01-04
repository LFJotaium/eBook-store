using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;

namespace ebookStore.Models
{
    public class User
    {
        [Required] // for name validation
        [StringLength(50, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 50 characters")]
        public string FirstName { get; set; }

        [StringLength(50, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 50 characters")]
        public string LastName { get; set; }

        [Key]
        [Required] // primary key 
        public string Username { get; set; }

        [Required] // Email validation
        [DataType(DataType.EmailAddress)]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public string Email { get; set; }

        [Required]
        [RegularExpression(@"^(?=.*[A-Z]).+$", ErrorMessage = "Password must contain at least one uppercase letter.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
        public string Password { get; set; }

        [Required]
        public string Role { get; set; } = "User"; // Default role is "User"

        public string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                // Generate a random salt
                var salt = GenerateSalt();
                var combinedPassword = Encoding.UTF8.GetBytes(password + salt); // Combine password with salt
                var hash = sha256.ComputeHash(combinedPassword);
                var hashString = Convert.ToBase64String(hash);

                return $"{hashString}:{salt}"; // Store hash and salt together
            }
        }

        private string GenerateSalt()
        {
            var rng = new RNGCryptoServiceProvider();
            var saltBytes = new byte[16]; // 128-bit salt
            rng.GetBytes(saltBytes);
            return Convert.ToBase64String(saltBytes);
        }
        // Make BorrowedBooks and PurchasedBooks nullable by setting them to null
        [NotMapped]
        public virtual ICollection<BorrowedBook> BorrowedBooks { get; set; } = null;

        [NotMapped]
        public virtual ICollection<PurchasedBook> PurchasedBooks { get; set; } = null;
    }
}
