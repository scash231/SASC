namespace SASC.Models
{
    public class SteamAccount
    {
        public string Username          { get; set; } = "";
        public string PersonaName       { get; set; } = "";
        public string SteamId           { get; set; } = "";
        public bool   IsRecent          { get; set; } = false;
        public bool   IsManual          { get; set; } = false;
        public string Password          { get; set; } = "";
        public string EncryptedPassword { get; set; } = "";
        public string Note              { get; set; } = "";

        public string DisplayName =>
            !string.IsNullOrEmpty(PersonaName) ? PersonaName : Username;

        public string Initials
        {
            get
            {
                var name = DisplayName;
                if (string.IsNullOrEmpty(name)) return "?";
                return name.Length >= 2
                    ? name.Substring(0, 2).ToUpper()
                    : name.Substring(0, 1).ToUpper();
            }
        }
    }
}
