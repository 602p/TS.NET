using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace TS.NET.Engine
{
    // The job of this task is to read from the thunderscope as fast as possible with minimal jitter
    internal class InputTask
    {
        private CancellationTokenSource? cancelTokenSource;
        private Task? taskLoop;

        public void Start(ILoggerFactory loggerFactory, Thunderscope scope, BlockingChannelReader<ThunderscopeMemory> memoryPool, BlockingChannelWriter<ThunderscopeMemory> processingPool)
        {
            var logger = loggerFactory.CreateLogger("InputTask");
            cancelTokenSource = new CancellationTokenSource();
            taskLoop = Task.Factory.StartNew(() => Loop(logger, scope, memoryPool, processingPool, cancelTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancelTokenSource?.Cancel();
            taskLoop?.Wait();
        }

        private static void Loop(ILogger logger, Thunderscope scope, BlockingChannelReader<ThunderscopeMemory> memoryPool, BlockingChannelWriter<ThunderscopeMemory> processingPool, CancellationToken cancelToken)
        {
            try
            {
                Thread.CurrentThread.Name = "TS.NET Input";
                Thread.CurrentThread.Priority = ThreadPriority.Highest;

                logger.LogDebug($"Thread ID: {Thread.CurrentThread.ManagedThreadId}");

                scope.EnableChannel(0);
                scope.EnableChannel(1);
                scope.EnableChannel(2);
                scope.EnableChannel(3);
                scope.Start();

                Stopwatch oneSecond = Stopwatch.StartNew();
                uint oneSecondEnqueueCount = 0;
                uint enqueueCounter = 0;

                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    var memory = memoryPool.Read();

                    while (true)
                    {
                        try
                        {
                            scope.Read(memory);
                            break;
                        }
                        catch (ThunderscopeMemoryOutOfMemoryException ex)
                        {
                            logger.LogWarning("Scope ran out of memory - reset buffer pointers and continue");
                            scope.ResetBuffer();
                            continue;
                        }
                        catch (ThunderscopeFIFOOverflowException ex)
                        {
                            logger.LogWarning("Scope had FIFO overflow - ignore and continue");
                            continue;
                        }
                        catch (ThunderscopeNotRunningException ex)
                        {
                            // logger.LogWarning("Tried to read from stopped scope");
                            continue;
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

                    oneSecondEnqueueCount++;
                    enqueueCounter++;

                    processingPool.Write(memory);

                    if (oneSecond.ElapsedMilliseconds >= 1000)
                    {
                        logger.LogDebug($"Enqueues/sec: {oneSecondEnqueueCount / (oneSecond.ElapsedMilliseconds * 0.001):F2}, enqueue count: {enqueueCounter}");
                        oneSecond.Restart();
                        oneSecondEnqueueCount = 0;
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
