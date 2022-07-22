﻿using Microsoft.Extensions.Logging;
using System;

namespace TS.NET.Engine
{
    // The job of this task is to read from the thunderscope as fast as possible with minimal jitter
    internal class InputTask
    {
        private CancellationTokenSource? cancelTokenSource;
        private Task? taskLoop;

        public void Start(ILoggerFactory loggerFactory, Thunderscope thunderscope, BlockingChannelReader<ThunderscopeMemory> memoryPool, BlockingChannelWriter<ThunderscopeMemory> processingPool)
        {
            var logger = loggerFactory.CreateLogger("InputTask");
            cancelTokenSource = new CancellationTokenSource();
            taskLoop = Task.Factory.StartNew(() => Loop(logger, thunderscope, memoryPool, processingPool, cancelTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancelTokenSource?.Cancel();
            taskLoop?.Wait();
        }

        private static void Loop(ILogger logger, Thunderscope thunderscope, BlockingChannelReader<ThunderscopeMemory> memoryPool, BlockingChannelWriter<ThunderscopeMemory> processingPool, CancellationToken cancelToken)
        {
            try
            {
                Thread.CurrentThread.Name = "TS.NET Input";
                Thread.CurrentThread.Priority = ThreadPriority.Highest;

                logger.LogDebug($"Thread ID: {Thread.CurrentThread.ManagedThreadId}");

                thunderscope.EnableChannel(0);
                thunderscope.EnableChannel(1);
                thunderscope.EnableChannel(2);
                thunderscope.EnableChannel(3);
                thunderscope.Start();

                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    try
                    {
                        lock (thunderscope) {
                            if (!thunderscope.Enabled) continue;
                            var memory = memoryPool.Read();
                            thunderscope.Read(memory);
                            processingPool.Write(memory);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message == "ReadFile - failed (1359)")
                        {
                            logger.LogError(ex, $"{nameof(InputTask)} error");
                            continue;
                        }
                        throw;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug($"{nameof(InputTask)} stopping");
                // throw;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, $"{nameof(InputTask)} error");
                throw;
            }
            finally
            {
                logger.LogDebug($"{nameof(InputTask)} stopped");
            }
        }
    }
}
