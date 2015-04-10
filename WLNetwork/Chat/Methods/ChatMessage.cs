namespace WLNetwork.Chat.Methods
{
    public class ChatMessage
    {
        public const string Msg = "onchatmessage";
        public string Id { get; set; }
        public string Member { get; set; }
        public string Text { get; set; }
        public bool Auto { get; set; }
    }
}