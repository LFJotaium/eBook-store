# eBook Library Service

## Overview
The eBook Library Service is a web-based application designed for managing an eBook store. The platform allows users to search, sort, borrow, and buy books in various formats. It includes an admin interface for managing books, users, and pricing. The project emphasizes features like dynamic pricing, waiting lists, secure payment processing, and personalized user libraries.

## Features

### **User Features**
- **Search & Filter**:
  - Search books by title, author, or publisher.
  - Filter by genre, price range, publication year, and discounted books.
  - Sort by price, popularity, and publication year.
- **Book Details**:
  - View book information
- **Borrowing Books**:
  - Borrow up to 3 books for 30 days.
  - Waitlist system for unavailable books with email notifications.
- **Buying Books**:
  - Buy books and download them in desired formats.
  - View purchased books in the personal library.
- **Payments**:
  - Pay using a shopping cart or "Buy Now" option.
  - Credit card and PayPal integration with secure processing.
- **Feedback**:
  - Rate and review books and the borrowing/buying experience.

### **Admin Features**
- Add, edit, and delete books.
- Manage book prices, discounts, and formats.
- Handle user registration and permissions.

### **Other Functionalities**
- Dynamic pricing adjustment after discount expiration.
- Personalized user libraries showing borrowed and purchased books.
- Reminder notifications 5 days before borrowed books are due.

## Technologies Used
- **Framework**: ASP.NET MVC
- **Database**: PostgreSQL
- **Language**: C#
- **Front-End**: Razor Pages, HTML, CSS
- **Payment Integration**: PayPal API

## Database Design
### Key Tables:
1. **Books**
   - `ID`: Primary key.
   - `Title`, `AuthorName`, `Publisher`, `YearOfPublish`, `Genre`, `CoverImagePath`.
2. **Prices**
   - `BookID`: Foreign key referencing `Books`.
   - `CurrentPriceBuy`, `CurrentPriceBorrow`, `OriginalPriceBuy`, `OriginalPriceBorrow`, `IsDiscounted`, `DiscountEndDate`.
3. **BookFormats**
   - `BookID`: Foreign key referencing `Books`.
   - `Format`: Stores formats like EPUB, FB2, MOBI, PDF.
4. **ShoppingCart**
   - `Username`: User ID.
   - `BookID`: Foreign key referencing `Books`.
5. **PurchasedBooks**
   - `Username`: User ID.
   - `BookID`: Foreign key referencing `Books`.

## Installation
1. Clone the repository:
   ```bash
   git clone <repository_url>
   ```
2. Open the project in Visual Studio.
3. Set up the PostgreSQL database using the provided SQL scripts.
4. Update the connection string in `appsettings.json`:
   ```json
   "ConnectionStrings": {
       "DefaultConnection": "Host=<HOST>;Database=<DB_NAME>;Username=<USER>;Password=<PASSWORD>;"
   }
   ```
5. Build and run the project.

## Usage
1. Register as a user or log in as an admin to access the full features.
2. Use the eBook gallery to browse and interact with the books.
3. Admins can manage books, prices, and users via the admin panel.


## Acknowledgments
- PayPal API documentation.
- ASP.NET MVC tutorials and guides.
- PostgreSQL community support.
