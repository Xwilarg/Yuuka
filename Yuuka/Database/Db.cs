using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

namespace Yuuka.Database
{
    public sealed class Db
    {
        public Db()
        {
            _r = RethinkDB.R;
        }

        public async Task InitAsync(string dbName)
        {
            _dbName = dbName;
            _conn = await _r.Connection().ConnectAsync();
            if (!await _r.DbList().Contains(_dbName).RunAsync<bool>(_conn))
                _r.DbCreate(_dbName).Run(_conn);

            _allTags = new Dictionary<ulong, GuildTags>();
            _uploadSize = new Dictionary<string, long>();
        }

        public async Task InitGuildAsync(SocketGuild guild)
        {
            if (_allTags.ContainsKey(guild.Id)) // Guild already loaded
                return;

            if (!await _r.Db(_dbName).TableList().Contains("Tags-" + guild.Id).RunAsync<bool>(_conn))
                _r.Db(_dbName).TableCreate("Tags-" + guild.Id).Run(_conn);

            var gTags = new GuildTags();
            foreach (JObject elem in await _r.Db(_dbName).Table("Tags-" + guild.Id).RunAsync(_conn))
            {
                // We load everything manually because we are not sure how to load "Content"
                string id = elem["id"].Value<string>();
                var type = (TagType)elem["Type"].Value<int>();
                string description = elem["Description"] == null ? "" : elem["Description"].Value<string>();
                string tag = elem["Key"].Value<string>();
                string user = elem["User"].Value<string>();
                string userId = elem["UserId"].Value<string>();
                bool isNsfw = elem["IsNsfw"].Value<bool>();
                DateTime creationTime = new DateTime(1970, 1, 1).AddSeconds(elem["CreationTime"]["epoch_time"].Value<double>());
                int nbUsage = elem["NbUsage"].Value<int>();
                string serverId = elem["ServerId"].Value<string>();
                object content;
                if (type == TagType.TEXT)
                    content = elem["Content"].Value<string>();
                else
                    content = (byte[])await _r.Binary(elem["Content"]).RunAsync<byte[]>(_conn);

                AddUploadSize(userId, content);

                string extension = elem["Extension"].Value<string>();
                gTags.Tags.Add(tag, new Tag(id, tag, description, type, user, userId, content, extension, isNsfw, creationTime, nbUsage, serverId));
            }

            _allTags.Add(guild.Id, gTags);
        }

        public void AddUploadSize(string userId, object content)
        {
            using (var s = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(s, content);
                if (!_uploadSize.ContainsKey(userId))
                    _uploadSize.Add(userId, s.Length);
                else
                    _uploadSize[userId] += s.Length;
            }
        }

        public void RemoveUploadSize(string userId, object content)
        {
            using (var s = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(s, content);
                if (_uploadSize.ContainsKey(userId))
                    _uploadSize[userId] -= s.Length;
            }
        }

        public long GetUploadSize(string userId)
        {
            return !_uploadSize.ContainsKey(userId) ? 0L : _uploadSize[userId];
        }

        public string CanDeleteTag(ulong guildId, ulong userId, string key)
        {
            var tags = _allTags[guildId];
            key = key.ToLower();
            if (!tags.Tags.ContainsKey(key))
                return "This tag doesn't exist.";
            if (tags.Tags[key].UserId != userId.ToString())
                return "This tag wasn't created by you";
            return null;
        }

        public async Task<bool> DeleteTagAsync(ulong guildId, ulong userId, string key)
        {
            if (CanDeleteTag(guildId, userId, key) != null)
                return false;

            var tags = _allTags[guildId];
            var curr = tags.Tags[key];
            key = key.ToLower();
            tags.Tags.Remove(key);
            await _r.Db(_dbName).Table("Tags-" + guildId).Filter(x => x["Key"] == key).Delete().RunAsync(_conn);
            RemoveUploadSize(userId.ToString(), curr.Content);
            return true;
        }

        // Try to add a tag, if it already exists it'll fail and return false
        public async Task<bool> AddTagAsync<T>(ulong guildId, TagType type, string key, IUser user, T content, string extension, string serverId)
        {
            var tags = _allTags[guildId];
            key = key.ToLower();
            if (tags.Tags.ContainsKey(key))
                return false;

            Tag tag = new Tag((string)await _r.Uuid(key).RunAsync(_conn), key, "", type, user.ToString(), user.Id.ToString(), content, extension, false, DateTime.UtcNow, 0, serverId);
            await _r.Db(_dbName).Table("Tags-" + guildId).Insert(tag).RunAsync(_conn);
            tags.Tags.Add(key, tag);
            AddUploadSize(user.Id.ToString(), (object)content);
            return true;
        }

        public Tag GetRandom(ulong guildId)
        {
            var tags = _allTags[guildId];
            if (tags.Tags.Values.Count == 0)
                return null;
            return new List<Tag>(tags.Tags.Values)[Program.P.Rand.Next(tags.Tags.Values.Count)];
        }

        public Tag GetRandomWithType(ulong guildId, TagType type)
        {
            var tags = new List<Tag>(_allTags[guildId].Tags.Values).Where(x => x.Type == type);
            if (tags.Count() == 0)
                return null;
            return tags.ElementAt(Program.P.Rand.Next(0, tags.Count()));
        }

        public async Task SetDescriptionAsync(ulong guildId, Tag tag, string description)
        {
            tag.Description = description;
            await _r.Db(_dbName).Table("Tags-" + guildId).Update(_r.HashMap("id", tag.id)
                .With("Description", description)
            ).RunAsync(_conn);
        }

        public string[] GetList(ulong guildId, int count)
        {
            return new List<Tag>(_allTags[guildId].Tags.Values).Select(x => x.Key).OrderBy(x => x).Take(100 * count).Skip(100 * count - 100).ToArray();
        }

        public string[] GetListWithType(ulong guildId, TagType type, int count)
        {
            var tags = new List<Tag>(_allTags[guildId].Tags.Values).Where(x => x.Type == type).OrderBy(x => x.Key);
            return tags.Select(x => x.Key).Take(100 * count).Skip(100 * count - 100).ToArray();
        }

        public Tag GetTag(ulong guildId, string key)
        {
            var tags = _allTags[guildId];
            key = key.ToLower();
            if (!tags.Tags.ContainsKey(key))
                return null;
            return tags.Tags[key];
        }

        public Tag SendTag(ulong guildId, string key)
        {
            var tags = _allTags[guildId];
            key = key.ToLower();
            if (!tags.Tags.ContainsKey(key))
                return null;
            var tag = tags.Tags[key];
            tag.NbUsage++;
            _r.Db(_dbName).Table("Tags-" + guildId).Update(_r.HashMap("id", tag.id)
                .With("NbUsage", tag.NbUsage)
            ).RunNoReply(_conn);
            return tags.Tags[key];
        }

        public int GetCount(ulong guildId, string userId)
            => new List<Tag>(_allTags[guildId].Tags.Values).Count(x => x.UserId == userId);

        public int Count(ulong guildId)
            => _allTags[guildId].Tags.Count;

        public int Count(ulong guildId, TagType type)
            => new List<Tag>(_allTags[guildId].Tags.Values).Count(x => x.Type == type);

        public int GetDescriptionCount(ulong guildId, string userId)
            => new List<Tag>(_allTags[guildId].Tags.Values).Count(x => x.UserId == userId && x.Description != "");

        private RethinkDB _r;
        private Connection _conn;
        private string _dbName;

        private Dictionary<ulong, GuildTags> _allTags;
        private Dictionary<string, long> _uploadSize;
    }
}
