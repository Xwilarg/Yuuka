namespace Yuuka.Database
{
    public class Guild
    {
        public Guild(string id)
        {
            this.id = id;
        }

        public string id;
        public string Prefix = ".";
        public string[] AllowedRoles = new string[0];
    }
}
