// https://github.com/cloudtoid/interprocess
using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using TS.NET.Memory.Unix;
using TS.NET.Memory.Windows;
using Microsoft.Extensions.Logging;
using TS.NET.Memory;
using System.Runtime.CompilerServices;

namespace TS.NET
{
    // This is a shared memory-mapped file between processes, with only a single writer and a single reader with a header struct
    // Not thread safe
    public interface IThunderscopeBridgeWriter : IDisposable
    {
        public Span<byte> GetAcquiringRegion();
        public ThunderscopeMonitoring GetMonitoring();

        public abstract void SetConfiguration(ThunderscopeConfiguration value);

        public abstract void MonitoringReset();

        public abstract void SwitchRegionIfNeeded();

        public abstract void DataWritten();
    }
}
