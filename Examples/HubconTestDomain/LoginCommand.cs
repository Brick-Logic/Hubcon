using System.ComponentModel.DataAnnotations;
using Hubcon;
using Microsoft.Extensions.DependencyInjection;

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

        [Required]
        public string Username { get; }
        
        [Required]
        public string Password { get; }
        
        [Required]
        public bool RememberMe { get; }
    }
}
