namespace ebookStore.Models
{
    public class WaitingListEntry
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public string Username { get; set; }
        public DateTime DateAdded { get; set; }

        public Book Book { get; set; }
    }
}