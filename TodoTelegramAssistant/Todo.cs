namespace TodoTelegramAssistant
{
    public class Todo
    {
        public int TodoId { get; set; }
        public string Title { get; set; }
        public User Owner { get; set; }
    }
}
