using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Models
{
    public class ClientReference
    {
        public string Id { get; }
        public object? ClientInfo { get; set; }

        public ClientReference(string id)
        {
            Id = id;
        }

        public ClientReference(string id, object? clientInfo)
        {
            Id = id;
            ClientInfo = clientInfo;
        }   
    }
}
