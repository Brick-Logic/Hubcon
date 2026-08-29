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
        [DefaultValue("username")]
        public string Username { get; } = username;

        [Required]
        [DefaultValue("password")]
        public string Password { get; } = password;

        [Required]
        [DefaultValue(true)]
        public bool RememberMe { get; } = rememberMe;

        [Required]
        public ValidationTestClass ValidationTest { get; } = validationTest;
    }

    public class ValidationTestClass(string username, ValidationTestClass2 validationTestClass2)
    {
        [NotNull]
        [DefaultValue("username")]
        public string Username { get; } = username;

        [Required]
        public ValidationTestClass2 ValidationTestClass2 { get; } = validationTestClass2;
    }
    
    public class ValidationTestClass2(string username2)
    {
        [NotNull]
        [DefaultValue("username2")]
        public string Username2 { get; } = username2;
    }
}
