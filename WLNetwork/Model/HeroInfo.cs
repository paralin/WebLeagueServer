using MongoDB.Bson.Serialization.Attributes;

namespace WLNetwork.Model
{
    /// <summary>
    /// Cached hero name data.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class HeroInfo
    {
        public uint Id { get; set; }
        public string name { get; set; }
        public string fullName { get; set; }
    }
}