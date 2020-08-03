using Newtonsoft.Json;

namespace Yuuka.Database
{
    public struct Tag
    {
        public Tag(string key, TagType type, string user, ulong userId, object content, string extension)
        {
            id = key.GetHashCode().ToString();
            Key = key;
            Type = type;
            User = user;
            UserId = userId;
            Content = content;
            Extension = extension;
        }

        [JsonProperty]
        public string id;
        [JsonProperty]
        public string Key;
        [JsonProperty]
        public TagType Type;
        [JsonProperty]
        public string User;
        [JsonProperty]
        public ulong UserId;
        [JsonProperty]
        public object Content;
        [JsonProperty]
        public string Extension;
    }
}
