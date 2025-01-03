namespace ebookStore.Models;

public class ShoppingCart
{
    public int ID { get; set; }
    public int UserId { get; set; }
    public int BookId { get; set; }
    public int Quantity { get; set; }
}

/*
 * Table struct:
       ID SERIAL PRIMARY KEY,
       UserId INT NOT NULL,
       BookId INT NOT NULL,
       Quantity INT DEFAULT 1,
       FOREIGN KEY (UserId) REFERENCES Users(ID),
       FOREIGN KEY (BookId) REFERENCES Books(ID)
 */
