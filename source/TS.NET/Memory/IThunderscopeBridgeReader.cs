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
        public Span<byte> GetAcquiredRegion();

        public void Dispose();

        public ThunderscopeConfiguration GetConfiguration();

        public ThunderscopeMonitoring GetMonitoring();

        public bool RequestAndWaitForData(int millisecondsTimeout);
    }
}
