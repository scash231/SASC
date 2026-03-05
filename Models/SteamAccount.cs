namespace SASC.Models
{
    public class SteamAccount
    {
        public string Username        { get; set; } = "";
        public string PersonaName     { get; set; } = "";
        public string SteamId         { get; set; } = "";
        public bool   IsRecent        { get; set; } = false;
        public bool   RememberPassword{ get; set; } = true;
        public bool   IsManual        { get; set; } = false;
        public string Password        { get; set; } = "";
        public string Note            { get; set; } = "";

        public string DisplayName => IsManual ? Username : PersonaName;
        public string Initials    => DisplayName.Length >= 2
            ? DisplayName[..2].ToUpper()
            : DisplayName.ToUpper();
    }
}
