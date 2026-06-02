using System;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IPingManager : IDisposable
    {
        void Start();
    }
}