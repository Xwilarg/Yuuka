using Discord;
using Newtonsoft.Json.Linq;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using System;
using System.Collections.Generic;
using System.Linq;
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
            if (!await _r.Db(_dbName).TableList().Contains("Tags").RunAsync<bool>(_conn))
                _r.Db(_dbName).TableCreate("Tags").Run(_conn);

            _globalTags = new Dictionary<string, Tag>();
            foreach (JObject elem in await _r.Db(_dbName).Table("Tags").RunAsync(_conn))
            {
                var type = (TagType)elem["Type"].Value<int>();
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
                {
                    content = (byte[])await _r.Binary(elem["Content"]).RunAsync<byte[]>(_conn);
                }
                string extension = elem["Extension"].Value<string>();
                _globalTags.Add(tag, new Tag(tag, type, user, userId, content, extension, isNsfw, creationTime, nbUsage, serverId));
            }
        }

        public async Task<bool> AddTagAsync<T>(TagType type, string key, IUser user, T content, string extension, string serverId)
        {
            key = key.ToLower();
            if (await _r.Db(_dbName).Table("Tags").GetAll(key.GetHashCode()).Count().Eq(1).RunAsync<bool>(_conn))
                return false;

            Tag tag = new Tag(key, type, user.ToString(), user.Id.ToString(), content, extension, false, DateTime.UtcNow, 0, serverId);
            await _r.Db(_dbName).Table("Tags").Insert(tag).RunAsync(_conn);
            _globalTags.Add(key, tag);
            return true;

        }

        public Tag GetRandom()
        {
            return new List<Tag>(_globalTags.Values)[Program.P.Rand.Next(_globalTags.Values.Count)];
        }

        public Tag GetRandomWithType(TagType type)
        {
            var tags = new List<Tag>(_globalTags.Values).Where(x => x.Type == type);
            return tags.ElementAt(Program.P.Rand.Next(0, tags.Count()));
        }

        public string[] GetList()
        {
            return new List<Tag>(_globalTags.Values).Select(x => x.Key).OrderBy(x => x).ToArray();
        }

        public string[] GetListWithType(TagType type)
        {
            var tags = new List<Tag>(_globalTags.Values).Where(x => x.Type == type).OrderBy(x => x);
            return tags.Select(x => x.Key).ToArray();
        }

        public Tag GetTag(string key)
        {
            if (!_globalTags.ContainsKey(key))
                return null;
            return _globalTags[key];
        }

        public async Task<Tag> SendTag(string key)
        {
            if (!_globalTags.ContainsKey(key))
                return null;
            var tag = _globalTags[key];
            tag.NbUsage++;
            await _r.Db(_dbName).Table("Tags").Update(tag).RunAsync(_conn);
            return _globalTags[key];
        }

        public int GetCount(string userId)
            => new List<Tag>(_globalTags.Values).Count(x => x.UserId == userId);

        private RethinkDB _r;
        private Connection _conn;
        private string _dbName;

        private Dictionary<string, Tag> _globalTags;
    }
}
