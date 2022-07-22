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

        public void Start(ILoggerFactory loggerFactory, Thunderscope scope, IThunderscopeBridgeReader bridge)
        {
            var logger = loggerFactory.CreateLogger("SocketTask");
            cancelTokenSource = new CancellationTokenSource();
            uint bufferLength = 4 * 100 * 1000 * 1000;      //Maximum record length = 100M samples per channel
            taskLoop = Task.Factory.StartNew(() => Loop(logger, scope, bridge, cancelTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancelTokenSource?.Cancel();
            taskLoop?.Wait();
        }

        private static void Loop(ILogger logger, Thunderscope scope, IThunderscopeBridgeReader bridge, CancellationToken cancelToken)
        {
            Thread.CurrentThread.Name = "TS.NET Socket";

            logger.LogDebug($"Thread ID: {Thread.CurrentThread.ManagedThreadId}");

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

                    // logger.LogDebug("Got request for waveform...");

                    while (true) {
                        cancelToken.ThrowIfCancellationRequested();

                        if (bridge.RequestAndWaitForData(500))
                        {
                            var cfg = bridge.GetConfiguration();
                            var data = bridge.GetAcquiredRegion();
                            ulong channelLength = (ulong)cfg.ChannelLength;

                            WaveformHeader header = new() {
                                seqnum = seqnum,
                                numChannels = 4,
                                fsPerSample = 1000000 * 4, // 1GS / 4 channels (?)
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

                                for (byte ch = 0; ch < 4; ch++) {
                                    ThunderscopeChannel tChannel = scope.Channels[ch];

                                    chHeader.chNum = ch;
                                    chHeader.scale = (float)((float)tChannel.VoltsDiv / 1000f * 10f) / 255f;
                                    chHeader.offset = -(float)tChannel.VoltsOffset;

                                    clientSocket.Send(new ReadOnlySpan<byte>(&chHeader, sizeof(ChannelHeader)));
                                    clientSocket.Send(data.Slice(ch * (int)channelLength, (int)channelLength));
                                }
                            }

                            seqnum++;
                            // string textInfo = JsonConvert.SerializeObject(cfg, Formatting.Indented, new Newtonsoft.Json.Converters.StringEnumConverter()); 
                            // logger.LogInfo(textInfo);
                            // Thread.Sleep(10);

                            break;
                        }

                        logger.LogDebug("Remote wanted waveform but not ready");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug($"{nameof(SocketTask)} stopping");
                // throw;
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
