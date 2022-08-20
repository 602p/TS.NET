using System;

namespace TS.NET.Engine
{
    public abstract record HardwareRequestDto();
    public record HardwareStartRequest() : HardwareRequestDto;
    public record HardwareStopRequest() : HardwareRequestDto;
    public record HardwareSetOffsetRequest(int Channel, double Offset) : HardwareRequestDto;
}
