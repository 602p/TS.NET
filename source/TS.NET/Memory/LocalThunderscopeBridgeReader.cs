using Microsoft.Extensions.Logging;
using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TS.NET.Memory.Unix;
using TS.NET.Memory.Windows;

using System.Diagnostics;

namespace TS.NET
{
    // This is a shared memory-mapped file between processes, with only a single writer and a single reader with a header struct
    public class LocalThunderscopeBridgeReader : IThunderscopeBridgeReader
    {
        private LocalThunderscopeBridgeWriter writer;

        public unsafe LocalThunderscopeBridgeReader(LocalThunderscopeBridgeWriter writer)
        {
            this.writer = writer;
        }

        public void Dispose()
        {
            
        }

        public ThunderscopeConfiguration GetConfiguration()
        {
            return writer.header.Configuration;
        }

        public ThunderscopeMonitoring GetMonitoring()
        {
            return writer.header.Monitoring;
        }

        public bool RequestAndWaitForData(int millisecondsTimeout)
        {
            writer.dataRequested = true;

            Stopwatch time = Stopwatch.StartNew();
            while (time.ElapsedMilliseconds < millisecondsTimeout) {
                if (writer.dataReady)
                    return true;
            }
            return false;
        }

        public unsafe ReadOnlySpan<byte> GetAcquiredRegion()
        {
            return writer.GetAcquiredRegion();
        }
    }
}
