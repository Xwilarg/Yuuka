using Discord;
using Newtonsoft.Json.Linq;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using System.Collections.Generic;
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
                ulong userId = elem["UserId"].Value<ulong>();
                object content;
                if (type == TagType.TEXT)
                    content = elem["Content"].Value<string>();
                else
                {
                    content = (byte[])await _r.Binary(elem["Content"]).RunAsync<byte[]>(_conn);
                }
                string extension = elem["Extension"].Value<string>();
                _globalTags.Add(tag, new Tag(tag, type, user, userId, content, extension));
            }
        }

        public async Task<bool> AddTagAsync<T>(TagType type, string key, IUser user, T content, string extension)
        {
            key = key.ToLower();
            if (await _r.Db(_dbName).Table("Tags").GetAll(key.GetHashCode()).Count().Eq(1).RunAsync<bool>(_conn))
                return false;

            Tag tag = new Tag(key, type, user.ToString(), user.Id, content, extension);
            await _r.Db(_dbName).Table("Tags").Insert(tag).RunAsync(_conn);
            _globalTags.Add(key, tag);
            return true;

        }

        public Tag? GetTag(string key)
        {
            if (!_globalTags.ContainsKey(key))
                return null;
            return _globalTags[key];
        }

        private RethinkDB _r;
        private Connection _conn;
        private string _dbName;

        private Dictionary<string, Tag> _globalTags;
    }
}
