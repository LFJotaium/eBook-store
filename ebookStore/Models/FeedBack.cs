namespace ebookStore.Models
{
    public class Feedback
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public string Username { get; set; }
        public string Comment { get; set; }
        public int Rating { get; set; }
        public DateTime FeedbackDate { get; set; }

        public Book Book { get; set; }
        public User User { get; set; }
    }
}