using Newtonsoft.Json;
using System;

namespace Yuuka.Database
{
    public class Tag
    {
        public Tag(string key, TagType type, string user, string userId, object content, string extension, bool isNsfw, DateTime creationTime, int nbUsage, string serverId)
        {
            id = key.GetHashCode().ToString();
            Key = key;
            Type = type;
            User = user;
            UserId = userId;
            Content = content;
            Extension = extension;
            IsNsfw = isNsfw;
            CreationTime = creationTime;
            NbUsage = nbUsage;
            ServerId = serverId;
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
        public string UserId;
        [JsonProperty]
        public object Content;
        [JsonProperty]
        public string Extension;
        [JsonProperty]
        public bool IsNsfw;
        [JsonProperty]
        public DateTime CreationTime;
        [JsonProperty]
        public int NbUsage;
        [JsonProperty]
        public string ServerId;
    }
}
