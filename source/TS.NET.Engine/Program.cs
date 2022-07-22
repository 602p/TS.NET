using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TS.NET;
using TS.NET.Engine;

Console.Title = "Engine";
using (Process p = Process.GetCurrentProcess())
    p.PriorityClass = ProcessPriorityClass.High;

using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole(options => { options.SingleLine = true; options.TimestampFormat = "HH:mm:ss "; }).AddFilter(level => level >= LogLevel.Debug));

int bufferCount = 512;

BlockingChannel<ThunderscopeMemory> memoryPool = new(bufferCount);
for (int i = 0; i < bufferCount; i++)        // 120 = about 1 seconds worth of samples at 1GSPS
    memoryPool.Writer.Write(new ThunderscopeMemory());

Thread.Sleep(100);

var devices = Thunderscope.IterateDevices();
if (devices.Count == 0)
    throw new Exception("No thunderscopes found");
Thunderscope thunderscope = new Thunderscope();
thunderscope.Open(devices[0]);

BlockingChannel<ThunderscopeMemory> processingPool = new(bufferCount);

uint bufferLength = 4 * 100 * 1000 * 1000;
LocalThunderscopeBridgeWriter bridgeWriter = new LocalThunderscopeBridgeWriter(new ThunderscopeBridgeOptions("ThunderScope.1", bufferLength), loggerFactory);
LocalThunderscopeBridgeReader bridgeReader = new LocalThunderscopeBridgeReader(bridgeWriter);

ProcessingTask processingTask = new();
processingTask.Start(loggerFactory, processingPool.Reader, memoryPool.Writer, bridgeWriter);

InputTask inputTask = new();
inputTask.Start(loggerFactory, thunderscope, memoryPool.Reader, processingPool.Writer);

SocketTask socketTask = new();
socketTask.Start(loggerFactory, thunderscope, bridgeReader);

SCPITask scpiTask = new();
scpiTask.Start(loggerFactory, thunderscope);

Console.WriteLine("Running... press any key to stop");
Console.ReadKey();

processingTask.Stop();
inputTask.Stop();
socketTask.Stop();
scpiTask.Stop();
thunderscope.Close();
