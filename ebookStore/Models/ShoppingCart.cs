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


/*
CREATE TABLE ShoppingCart (
       ID SERIAL PRIMARY KEY,
       UserId VARCHAR(255) NOT NULL,  -- Updated to VARCHAR to match 'Users' table 'username'
       BookId INT NOT NULL,  -- Stays the same to match 'Books' table 'id'
       Quantity INT DEFAULT 1,
       CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
       FOREIGN KEY (UserId) REFERENCES Users(username),  -- Reference 'username' in Users table
       FOREIGN KEY (BookId) REFERENCES Books(id)  -- Reference 'id' in Books table
   );

 */
