namespace ebookStore.Models
{
    public class WaitingList
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public string Username { get; set; }
        public DateTime queuetime { get; set; }
    }
}