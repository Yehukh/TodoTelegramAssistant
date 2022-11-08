namespace TodoTelegramAssistant
{
    public class User
    {
        public int Id { get; set; }
        public long OwnerId { get; set; }
        public Localization Localization { get; set; }
    }
    public enum Localization
    {
        UA,
        US
    }
}
