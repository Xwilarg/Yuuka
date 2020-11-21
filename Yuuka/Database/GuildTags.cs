using System.Collections.Generic;

namespace Yuuka.Database
{
    public class GuildTags
    {
        public GuildTags()
        {
            Tags = new Dictionary<string, Tag>();
        }

        public Dictionary<string, Tag> Tags;
    }
}
