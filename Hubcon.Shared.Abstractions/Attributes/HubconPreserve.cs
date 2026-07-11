using System;

namespace Hubcon
{
    /// <summary>
    /// Attribute used to preserve types for Native AOT.
    /// <br/><br/>Usage:
    /// <br/>Use in a class to preserve that class and its children.
    /// <br/>Use in an interface to preserve all classes implementing that interface.
    /// </summary>
    public sealed class HubconPreserveAttribute : Attribute
    {
    }
}