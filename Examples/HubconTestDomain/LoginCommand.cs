using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Hubcon;
using Microsoft.Extensions.DependencyInjection;

namespace HubconTestDomain
{
    public class LoginCommand(string username, string password, bool rememberMe, ValidationTestClass validationTest)
    {
        [Required]
        public string Username { get; } = username;
        
        [Required]
        public string Password { get; } = password;
        
        [Required]
        public bool RememberMe { get; } = rememberMe;
        
        [NotNull]
        public ValidationTestClass ValidationTest { get; } = validationTest;
    }

    public class ValidationTestClass(string username, ValidationTestClass2 validationTestClass2)
    {
        [NotNull]
        public string Username { get; } = username;
        
        [NotNull]
        public ValidationTestClass2 ValidationTestClass2 { get; } = validationTestClass2;
    }
    
    public class ValidationTestClass2(string username2)
    {
        [NotNull]
        public string Username2 { get; } = username2;
    }
}
