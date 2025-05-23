using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Models.CustomAttributes
{
    public class HubconSubscriptionAttribute : HubconMethodAttribute
    {
        public HubconSubscriptionAttribute() : base(MethodType.Subscription)
        {

        }
    }
}