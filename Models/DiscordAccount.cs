namespace SASC.Models
{
    public class DiscordAccount
    {
        public string Username    { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Token       { get; set; } = "";
        public string Note        { get; set; } = "";
        public bool   IsActive    { get; set; } = false;

        public string Initials => string.IsNullOrEmpty(DisplayName)
            ? (Username.Length > 0 ? Username[0].ToString().ToUpper() : "?")
            : DisplayName[0].ToString().ToUpper();
    }
}
