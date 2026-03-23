using System;
#pragma warning disable CS1591
using System.Buffers;

namespace Hubcon.Shared.Core.Websockets.Models
{
    public sealed class TrimmedMemoryOwner : IMemoryOwner<byte>
    {
        private readonly IMemoryOwner<byte> _inner;
        private readonly int _length;

        public TrimmedMemoryOwner(IMemoryOwner<byte> inner, int length)
        {
            _inner = inner;
            _length = length;
        }

        public Memory<byte> Memory => _inner.Memory.Slice(0, _length);

        public void Dispose() => _inner.Dispose();
    }

}
