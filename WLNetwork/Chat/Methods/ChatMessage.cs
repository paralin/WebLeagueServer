using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WLNetwork.Chat.Methods
{
    [BsonIgnoreExtraElements]
    public class ChatMessage
    {
        public const string Msg = "onchatmessage";

        public BsonObjectId Id { get; set; }

        [BsonIgnore]
        public string ChatId { get; set; }
        public string Member { get; set; }
        public string Text { get; set; }
        public string Channel { get; set; }
        public bool Auto { get; set; }
        public DateTime Date { get; set; }
    }
}