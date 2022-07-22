using Microsoft.Extensions.Logging;
using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TS.NET.Memory.Unix;
using TS.NET.Memory.Windows;

namespace TS.NET
{
    // This is a shared memory-mapped file between processes, with only a single writer and a single reader with a header struct
    public interface IThunderscopeBridgeReader : IDisposable
    {
        public abstract ReadOnlySpan<byte> GetAcquiredRegion();

        public ThunderscopeConfiguration GetConfiguration();

        public ThunderscopeMonitoring GetMonitoring();

        public abstract bool RequestAndWaitForData(int millisecondsTimeout);
    }
}
