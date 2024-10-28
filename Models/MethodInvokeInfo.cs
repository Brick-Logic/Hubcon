using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Models
{
    [MessagePackObject]
    public record MethodInvokeInfo([property: Key(0)] string MethodName, [property: Key(1)] object?[] Args);
}
