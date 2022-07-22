// https://github.com/cloudtoid/interprocess
using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using TS.NET.Memory.Unix;
using TS.NET.Memory.Windows;
using Microsoft.Extensions.Logging;
using TS.NET.Memory;
using System.Runtime.CompilerServices;
using System.Threading;

namespace TS.NET
{
    // This is a shared memory-mapped file between processes, with only a single writer and a single reader with a header struct
    // Not thread safe
    public class LocalThunderscopeBridgeWriter : IThunderscopeBridgeWriter
    {
        private readonly ThunderscopeBridgeOptions options;
        private readonly ulong dataCapacityInBytes;
        private readonly IMemoryFile file;
        private readonly MemoryMappedViewAccessor view;
        private unsafe byte* regionA;
        private unsafe byte* regionB;
        internal ThunderscopeBridgeHeader header;
        internal bool dataRequested = false;
        private bool acquiringRegionFilled = false;
        internal bool dataReady = false;

        public unsafe Span<byte> GetAcquiringRegion() {
            return header.AcquiringRegion switch
            {
                ThunderscopeMemoryAcquiringRegion.RegionA => new Span<byte>(regionA, (int)dataCapacityInBytes),
                ThunderscopeMemoryAcquiringRegion.RegionB => new Span<byte>(regionB, (int)dataCapacityInBytes),
                _ => throw new InvalidDataException("Enum value not handled, add enum value to switch")
            };
        }

        internal unsafe ReadOnlySpan<byte> GetAcquiredRegion() {
            return header.AcquiringRegion switch
            {
                ThunderscopeMemoryAcquiringRegion.RegionA => new ReadOnlySpan<byte>(regionB, (int)dataCapacityInBytes),
                ThunderscopeMemoryAcquiringRegion.RegionB => new ReadOnlySpan<byte>(regionA, (int)dataCapacityInBytes),
                _ => throw new InvalidDataException("Enum value not handled, add enum value to switch")
            };
        }

        public ThunderscopeMonitoring GetMonitoring() {
            return header.Monitoring;
        }

        public unsafe LocalThunderscopeBridgeWriter(ThunderscopeBridgeOptions options, ILoggerFactory loggerFactory)
        {
            this.options = options;
            dataCapacityInBytes = options.BridgeCapacityBytes - (uint)sizeof(ThunderscopeBridgeHeader);
            
            header.AcquiringRegion = ThunderscopeMemoryAcquiringRegion.RegionA;
            header.Version = 1;
            header.DataCapacityBytes = dataCapacityInBytes;

            unsafe {
                regionA = (byte*)NativeMemory.AlignedAlloc((uint)dataCapacityInBytes, 4096);
                regionB = (byte*)NativeMemory.AlignedAlloc((uint)dataCapacityInBytes, 4096);
            }
        }

        public void Dispose()
        {
            unsafe {
                NativeMemory.AlignedFree(regionA);
                NativeMemory.AlignedFree(regionB);
            }
        }

        public void SetConfiguration(ThunderscopeConfiguration value)
        {
            // This is a shallow copy, but considering the struct should be 100% blitable (i.e. no reference types), this is effectively a full copy
            header.Configuration = value;
        }

        public void MonitoringReset()
        {
            header.Monitoring.TotalAcquisitions = 0;
            header.Monitoring.MissedAcquisitions = 0;
        }

        public void SwitchRegionIfNeeded()
        {
            if (dataRequested && acquiringRegionFilled) // UI has requested data and there is data available to be read...
            {
                // dataRequestSemaphore.Wait(); // Known not to block from above
                dataRequested = false;
                acquiringRegionFilled = false;
                header.AcquiringRegion = header.AcquiringRegion switch
                {
                    ThunderscopeMemoryAcquiringRegion.RegionA => ThunderscopeMemoryAcquiringRegion.RegionB,
                    ThunderscopeMemoryAcquiringRegion.RegionB => ThunderscopeMemoryAcquiringRegion.RegionA,
                    _ => throw new InvalidDataException("Enum value not handled, add enum value to switch")
                };

                dataReady = true;
            }
        }

        public void DataWritten()
        {
            header.Monitoring.TotalAcquisitions++;
            if (acquiringRegionFilled)
                header.Monitoring.MissedAcquisitions++;
            acquiringRegionFilled = true;
        }
    }
}
