namespace HubconTestDomain
{
    public record LoginCommand
    {
        public LoginCommand(string Username, string Password, bool RememberMe)
        {
            this.Username = Username;
            this.Password = Password;
            this.RememberMe = RememberMe;
        }

        public string Username { get; }
        public string Password { get; }
        public bool RememberMe { get; }
    }
}
