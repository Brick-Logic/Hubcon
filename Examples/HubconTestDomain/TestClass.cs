using Castle.DynamicProxy;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Attributes;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HubconTestDomain
{
    public class TestClass
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }
}
