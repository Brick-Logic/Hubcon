using Microsoft.AspNetCore.Authorization;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IDescriptor
    {
        public string DescriptorSignature { get; }
        public string ContractName { get; }
        public List<AuthorizeAttribute> Authorizations { get; }
        public bool NeedsAuthorization { get; }
    }
}
