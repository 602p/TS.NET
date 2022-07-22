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
    public class IntraprocessThunderscopeBridgeReader : IThunderscopeBridgeReader
    {
        private readonly ThunderscopeBridgeOptions options;
        private readonly ulong dataCapacityInBytes;
        private unsafe byte* basePointer;
        private unsafe byte* dataPointer { get; }
        private ThunderscopeBridgeHeader header;
        private bool IsHeaderSet { get { GetHeader(); return header.Version != 0; } }
        private readonly IInterprocessSemaphoreReleaser dataRequestSemaphore;
        private readonly IInterprocessSemaphoreWaiter dataReadySemaphore;

        public unsafe IntraprocessThunderscopeBridgeReader(ThunderscopeBridgeOptions options, byte* basePointer, byte* dataPointer)
        {
            this.options = options;
            dataCapacityInBytes = options.BridgeCapacityBytes - (uint)sizeof(ThunderscopeBridgeHeader);
            this.basePointer = basePointer;
            this.dataPointer = dataPointer;
            GetHeader();
            if (header.DataCapacityBytes != options.DataCapacityBytes)
                throw new Exception($"Mismatch in data capacity, options: {options.DataCapacityBytes}, bridge: {header.DataCapacityBytes}");
            dataRequestSemaphore = InterprocessSemaphore.CreateReleaser(options.MemoryName + "DataRequest");
            dataReadySemaphore = InterprocessSemaphore.CreateWaiter(options.MemoryName + "DataReady");
        }

        public void Dispose()
        {
        }

        public ThunderscopeConfiguration GetConfiguration()
        {
            GetHeader();
            return header.Configuration;
        }

        public ThunderscopeMonitoring GetMonitoring()
        {
            GetHeader();
            return header.Monitoring;
        }

        public bool RequestAndWaitForData(int millisecondsTimeout)
        {
            dataRequestSemaphore.Release();
            return dataReadySemaphore.Wait(millisecondsTimeout);
        }

        private void GetHeader()
        {
            unsafe { Unsafe.Copy(ref header, basePointer); }
        }

        //private void SetHeader()
        //{
        //    unsafe { Unsafe.Copy(basePointer, ref header); }
        //}

        public Span<byte> GetAcquiredRegion()
        {
            unsafe {
                int regionLength = (int)dataCapacityInBytes / 2;
                return header.AcquiringRegion switch
                {
                    ThunderscopeMemoryAcquiringRegion.RegionA => new Span<byte>(dataPointer + regionLength, regionLength),        // If acquiring region is Region A, return Region B
                    ThunderscopeMemoryAcquiringRegion.RegionB => new Span<byte>(dataPointer, regionLength),                       // If acquiring region is Region B, return Region A
                    _ => throw new InvalidDataException("Enum value not handled, add enum value to switch")
                };
            }
        }
    }
}
