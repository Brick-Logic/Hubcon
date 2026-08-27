using System.ComponentModel.DataAnnotations;
using Hubcon;
using Microsoft.Extensions.DependencyInjection;

namespace HubconTestDomain
{
    public record LoginCommand
    {
        public LoginCommand(string Username, string Password, bool RememberMe, ValidationTestClass validationTest)
        {
            this.Username = Username;
            this.Password = Password;
            this.RememberMe = RememberMe;
            ValidationTest = validationTest;
        }

        [Required]
        [System.ComponentModel.DataAnnotations.EmailAddress]
        public string Username { get; }
        
        [Required]
        public string Password { get; }
        
        [Required]
        public bool RememberMe { get; }
        
        [Required]
        public ValidationTestClass ValidationTest { get; }
    }

    public class ValidationTestClass
    {
        public ValidationTestClass(string username, ValidationTestClass2 validationTestClass2)
        {
            Username = username;
            ValidationTestClass2 = validationTestClass2;
        }

        [Required]
        [System.ComponentModel.DataAnnotations.EmailAddress]
        public string Username { get; }
        
        [Required]
        public ValidationTestClass2 ValidationTestClass2 { get; }
    }
    
    public class ValidationTestClass2
    {
        [Required]
        [System.ComponentModel.DataAnnotations.EmailAddress]
        public string Username2 { get; }
    }
}
