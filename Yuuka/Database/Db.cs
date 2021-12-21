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
            _guilds = new Dictionary<ulong, Guild>();
        }

        public async Task InitAsync(string dbName)
        {
            _dbName = dbName;
            _conn = await _r.Connection().ConnectAsync();
            if (!await _r.DbList().Contains(_dbName).RunAsync<bool>(_conn))
                _r.DbCreate(_dbName).Run(_conn);
            if (!await _r.Db(_dbName).TableList().Contains("Guilds").RunAsync<bool>(_conn))
                _r.Db(_dbName).TableCreate("Guilds").Run(_conn);

            _allTags = new Dictionary<ulong, GuildTags>();
            _uploadSize = new Dictionary<string, long>();
            _uploadSizeGuild = new Dictionary<string, long>();
        }

        public async Task InitGuildAsync(SocketGuild sGuild)
        {
            if (_allTags.ContainsKey(sGuild.Id)) // Guild already loaded
                return;

            Guild guild;
            if (await _r.Db(_dbName).Table("Guilds").GetAll(sGuild.Id.ToString()).Count().Eq(0).RunAsync<bool>(_conn)) // Guild doesn't exist in db
            {
                guild = new Guild(sGuild.Id.ToString());
                await _r.Db(_dbName).Table("Guilds").Insert(guild).RunAsync(_conn);
            }
            else
            {
                guild = await _r.Db(_dbName).Table("Guilds").Get(sGuild.Id.ToString()).RunAsync<Guild>(_conn);
            }
            _guilds.Add(sGuild.Id, guild);

            if (!await _r.Db(_dbName).TableList().Contains("Tags-" + sGuild.Id).RunAsync<bool>(_conn))
                _r.Db(_dbName).TableCreate("Tags-" + sGuild.Id).Run(_conn);

            var gTags = new GuildTags();
            foreach (JObject elem in await _r.Db(_dbName).Table("Tags-" + sGuild.Id).RunAsync(_conn))
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

                AddUploadSize(userId, serverId, content);

                string extension = elem["Extension"].Value<string>();
                gTags.Tags.Add(tag, new Tag(id, tag, description, type, user, userId, content, extension, isNsfw, creationTime, nbUsage, serverId));
            }

            _allTags.Add(sGuild.Id, gTags);
        }

        public void AddUploadSize(string userId, string guildId, object content)
        {
            using (var s = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(s, content);
                if (!_uploadSize.ContainsKey(userId))
                    _uploadSize.Add(userId, s.Length);
                else
                    _uploadSize[userId] += s.Length;
                if (!_uploadSizeGuild.ContainsKey(guildId))
                    _uploadSizeGuild.Add(guildId, s.Length);
                else
                    _uploadSizeGuild[guildId] += s.Length;
            }
        }

        public void RemoveUploadSize(string userId, string guildId, object content)
        {
            using (var s = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(s, content);
                if (_uploadSize.ContainsKey(userId))
                    _uploadSize[userId] -= s.Length;
                if (_uploadSizeGuild.ContainsKey(guildId))
                    _uploadSizeGuild[guildId] -= s.Length;
            }
        }

        public long GetAllUploadSize()
        {
            return _uploadSize.Values.Sum();
        }

        public long GetUploadSize(string userId)
        {
            return !_uploadSize.ContainsKey(userId) ? 0L : _uploadSize[userId];
        }

        public long GetGuildUploadSize(string guildId)
        {
            return !_uploadSizeGuild.ContainsKey(guildId) ? 0L : _uploadSizeGuild[guildId];
        }

        public string CanDeleteTag(IGuild guild, IGuildUser user, string key)
        {
            var tags = _allTags[guild.Id];
            key = key.ToLower();
            if (!tags.Tags.ContainsKey(key))
                return "This tag doesn't exist.";
            if (tags.Tags[key].UserId != user.Id.ToString() && (user.Id != guild.OwnerId || user.GuildPermissions.ManageGuild))
                return "This tag wasn't created by you";
            return null;
        }

        public async Task<bool> DeleteTagAsync(IGuild guild, IGuildUser user, string key)
        {
            if (CanDeleteTag(guild, user, key) != null)
                return false;

            var tags = _allTags[guild.Id];
            key = key.ToLower();
            var curr = tags.Tags[key];
            tags.Tags.Remove(key);
            await _r.Db(_dbName).Table("Tags-" + guild.Id).Filter(x => x["Key"] == key).Delete().RunAsync(_conn);
            RemoveUploadSize(user.Id.ToString(), guild.Id.ToString(), curr.Content);
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
            AddUploadSize(user.Id.ToString(), guildId.ToString(), (object)content);
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

        public string[] GetList(ulong guildId, string userId, int count, bool onlyMine)
        {
            return new List<Tag>(_allTags[guildId].Tags.Values).Where(x => !onlyMine || x.UserId == userId).Select(x => x.Key).OrderBy(x => x).Take(100 * count).Skip(100 * count - 100).ToArray();
        }

        public string[] GetListWithType(ulong guildId, string userId, TagType type, int count, bool onlyMine)
        {
            var tags = new List<Tag>(_allTags[guildId].Tags.Values).Where(x => x.Type == type && (!onlyMine || x.UserId == userId)).OrderBy(x => x.Key);
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

        public async Task UpdatePrefixAsync(ulong guildId, string prefix)
        {
            _guilds[guildId].Prefix = prefix;
            await _r.Db(_dbName).Table("Guilds").Update(_r.HashMap("id", guildId.ToString())
                .With("Prefix", prefix)
            ).RunAsync(_conn);
        }
        public async Task UpdateWhitelistAsync(ulong guildId, string[] whitelist)
        {
            _guilds[guildId].AllowedRoles = whitelist;
            await _r.Db(_dbName).Table("Guilds").Update(_r.HashMap("id", guildId.ToString())
                .With("AllowedRoles", whitelist)
            ).RunAsync(_conn);
        }


        public Guild GetGuild(ulong id) => _guilds[id];

        private RethinkDB _r;
        private Connection _conn;
        private string _dbName;

        private Dictionary<ulong, Guild> _guilds;
        private Dictionary<ulong, GuildTags> _allTags;
        private Dictionary<string, long> _uploadSize;
        private Dictionary<string, long> _uploadSizeGuild;
    }
}
