using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Hubcon;
using Microsoft.Extensions.DependencyInjection;

namespace HubconTestDomain
{
    public class LoginCommand(string username, string password, bool rememberMe, ValidationTestClass validationTest)
    {
        public string Username { get; } = username;
        
        public string Password { get; } = password;
        
        public bool RememberMe { get; } = rememberMe;
        
        public ValidationTestClass ValidationTest { get; } = validationTest;
    }

    public class ValidationTestClass(string username, ValidationTestClass2 validationTestClass2)
    {
        public string Username { get; } = username;
        
        public ValidationTestClass2 ValidationTestClass2 { get; } = validationTestClass2;
    }
    
    public class ValidationTestClass2(string username2)
    {
        public string Username2 { get; } = username2;
    }
}
