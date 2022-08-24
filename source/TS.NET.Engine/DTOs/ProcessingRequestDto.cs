using System;

namespace TS.NET.Engine
{
    public abstract record ProcessingRequestDto();

    public record ProcessingStartTriggerDto() : ProcessingRequestDto;
    public record ProcessingStopTriggerDto() : ProcessingRequestDto;
    public record ProcessingSingleTriggerDto() : ProcessingRequestDto;
    public record ProcessingForceTriggerDto() : ProcessingRequestDto;

    public record ProcessingSetDepthDto(long Samples) : ProcessingRequestDto;
    public record ProcessingSetRateDto(long SamplingHz) : ProcessingRequestDto;

    public record ProcessingSetTriggerSourceDto(int Channel) : ProcessingRequestDto;
    public record ProcessingSetTriggerDelayDto(long Femtoseconds) : ProcessingRequestDto;
    public record ProcessingSetTriggerLevelDto(double Level) : ProcessingRequestDto;
    public record ProcessingSetTriggerEdgeDirectionDto() : ProcessingRequestDto;

}
