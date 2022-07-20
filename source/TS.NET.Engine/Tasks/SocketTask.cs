using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace TS.NET.Engine
{
    internal class SocketTask
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]      
        internal struct WaveformHeader
        {
            internal UInt32 seqnum;
            internal UInt16 numChannels;
            internal UInt64 fsPerSample;
            internal UInt64 triggerFs;
            internal double hwWaveformsPerSec;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]      
        internal struct ChannelHeader
        {
            internal byte chNum;
            internal UInt64 depth;
            internal float scale;
            internal float offset;
            internal float trigphase;
            internal byte clipping;
        }

        private CancellationTokenSource? cancelTokenSource;
        private Task? taskLoop;

        public void Start(ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger("SocketTask");
            cancelTokenSource = new CancellationTokenSource();
            uint bufferLength = 4 * 100 * 1000 * 1000;      //Maximum record length = 100M samples per channel
            ThunderscopeBridgeReader bridge = new(new ThunderscopeBridgeOptions("ThunderScope.1", bufferLength), loggerFactory);
            taskLoop = Task.Factory.StartNew(() => Loop(logger, bridge, cancelTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancelTokenSource?.Cancel();
            taskLoop?.Wait();
        }

        private static void Loop(ILogger logger, ThunderscopeBridgeReader bridge, CancellationToken cancelToken)
        {
            Thread.CurrentThread.Name = "TS.NET Socket";

            Socket clientSocket = null;

            try
            {
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 5026);
             
                Socket listener = new Socket(IPAddress.Any.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
             
                listener.LingerState = new LingerOption (true, 1);
                listener.Bind(localEndPoint);

                logger.LogInformation("Starting data plane socket server at :5026");

                listener.Listen(10);
         
                clientSocket = listener.Accept();

                clientSocket.NoDelay = true;

                logger.LogInformation("Client connected to data plane");

                uint seqnum = 0;

                while (true)
                {
                    byte[] bytes = new Byte[1];
         
                    // Wait for flow control 'K'
                    while (true) {
                        cancelToken.ThrowIfCancellationRequested();

                        if (!clientSocket.Poll(10_000, SelectMode.SelectRead)) continue;
         
                        int numByte = clientSocket.Receive(bytes);
                         
                        if (numByte != 0) break;
                    }

                    logger.LogInformation("Got request for waveform...");

                    while (true) {
                        cancelToken.ThrowIfCancellationRequested();

                        if (bridge.RequestAndWaitForData(500))
                        {
                            ulong channelLength = (ulong)bridge.Configuration.ChannelLength;

                            uint viewportLength = 1000000;// (uint)upDownIndex.Value;
                            if (viewportLength < 100)
                                viewportLength = 100;
                            if (viewportLength > 10000000)
                                viewportLength = (uint)channelLength;

                            var cfg = bridge.Configuration;
                            var data = bridge.AcquiredRegion;
                            // int offset = (int)((channelLength / 2) - (viewportLength / 2));

                            WaveformHeader header = new() {
                                seqnum = seqnum,
                                numChannels = 1,
                                fsPerSample = 1000,
                                triggerFs = 0,
                                hwWaveformsPerSec = 1
                            };

                            ChannelHeader chHeader = new() {
                                chNum = 0,
                                depth = channelLength,
                                scale = 1,
                                offset = 0,
                                trigphase = 0,
                                clipping = 0
                            };

                            unsafe {
                                clientSocket.Send(new ReadOnlySpan<byte>(&header, sizeof(WaveformHeader)));
                                clientSocket.Send(new ReadOnlySpan<byte>(&chHeader, sizeof(ChannelHeader)));
                                clientSocket.Send(data.Slice(0, (Int32)channelLength));
                            }

                            seqnum++;
                            // string textInfo = JsonConvert.SerializeObject(cfg, Formatting.Indented, new Newtonsoft.Json.Converters.StringEnumConverter()); 
                            // logger.LogInfo(textInfo);
                            // Thread.Sleep(10);

                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug($"{nameof(SocketTask)} stopping");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, $"{nameof(SocketTask)} error");
                throw;
            }
            finally
            {
                try{
                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Close();
                } catch (Exception ex) {}

                logger.LogDebug($"{nameof(SocketTask)} stopped");
            }
        }
    }
}
