using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Client.Core.Configurations
{
    public class OperationOptions(MemberInfo memberInfo) : IOperationConfigurator, IOperationOptions
    {
        public MemberInfo MemberInfo { get; } = memberInfo;

        public MemberType MemberType { get; } = memberInfo switch
        {
            MethodInfo => MemberType.Method,
            PropertyInfo => MemberType.Property,
            _ => throw new ArgumentException("Unsupported member type", nameof(memberInfo))
        };

        public TransportType TransportType { get; private set; } = TransportType.Default;

        public IOperationConfigurator UseTransport(TransportType transportType)
        {
            TransportType = transportType;
            return this;
        }
    }
}
