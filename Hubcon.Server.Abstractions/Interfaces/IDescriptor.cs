using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
