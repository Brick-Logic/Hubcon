using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Models
{
    public class ClientReference<T>
    {
        public string Id { get; }
        public T? ClientInfo { get; set; }

        public ClientReference(string id)
        {
            Id = id;
        }

        public ClientReference(string id, T? clientInfo)
        {
            Id = id;
            ClientInfo = clientInfo;
        }   
    }
}
