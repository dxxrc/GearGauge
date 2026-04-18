using System.Runtime.InteropServices;
using GearGauge.Core.Models;

namespace GearGauge.Hardware;

internal static class DwmTimingReader
{
    public static OptionalFloat TryReadDisplayRefreshRate()
    {
        var timing = new DwmTimingInfo
        {
            cbSize = (uint)Marshal.SizeOf<DwmTimingInfo>()
        };

        var hr = DwmGetCompositionTimingInfo(IntPtr.Zero, ref timing);
        if (hr < 0 || timing.rateRefresh.uiDenominator == 0)
        {
            return OptionalFloat.None;
        }

        return (float)timing.rateRefresh.uiNumerator / timing.rateRefresh.uiDenominator;
    }

    public static OptionalFloat TryReadFramesPerSecond(nint hwnd, ref DwmFrameSample? previousSample)
    {
        if (!TryReadFrameCount(hwnd, out var currentFrameCount))
        {
            return OptionalFloat.None;
        }

        var currentSample = new DwmFrameSample(currentFrameCount, DateTimeOffset.UtcNow);
        if (previousSample is null)
        {
            previousSample = currentSample;
            return OptionalFloat.None;
        }

        var elapsed = currentSample.TimestampUtc - previousSample.Value.TimestampUtc;
        var frameDelta = currentFrameCount >= previousSample.Value.FrameCount
            ? currentFrameCount - previousSample.Value.FrameCount
            : 0UL;

        previousSample = currentSample;

        if (elapsed <= TimeSpan.FromMilliseconds(250) || frameDelta == 0)
        {
            return OptionalFloat.None;
        }

        var fps = (float)(frameDelta / elapsed.TotalSeconds);
        return fps is > 0 and < 1000 ? fps : OptionalFloat.None;
    }

    private static bool TryReadFrameCount(nint hwnd, out ulong frameCount)
    {
        var timing = new DwmTimingInfo
        {
            cbSize = (uint)Marshal.SizeOf<DwmTimingInfo>()
        };

        var hr = DwmGetCompositionTimingInfo(hwnd, ref timing);
        if (hr < 0)
        {
            frameCount = 0;
            return false;
        }

        frameCount = timing.cFramesDisplayed;
        return frameCount > 0;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetCompositionTimingInfo(nint hwnd, ref DwmTimingInfo timingInfo);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct UnsignedRatio
    {
        public uint uiNumerator;
        public uint uiDenominator;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DwmTimingInfo
    {
        public uint cbSize;
        public UnsignedRatio rateRefresh;
        public ulong qpcRefreshPeriod;
        public UnsignedRatio rateCompose;
        public ulong qpcVBlank;
        public ulong cRefresh;
        public uint cDXRefresh;
        public ulong qpcCompose;
        public ulong cFrame;
        public uint cDXPresent;
        public ulong cRefreshFrame;
        public ulong cFrameSubmitted;
        public uint cDXPresentSubmitted;
        public ulong cFrameConfirmed;
        public uint cDXPresentConfirmed;
        public ulong cRefreshConfirmed;
        public uint cDXRefreshConfirmed;
        public ulong cFramesLate;
        public uint cFramesOutstanding;
        public ulong cFrameDisplayed;
        public ulong qpcFrameDisplayed;
        public ulong cRefreshFrameDisplayed;
        public ulong cFrameComplete;
        public ulong qpcFrameComplete;
        public ulong cFramePending;
        public ulong qpcFramePending;
        public ulong cFramesDisplayed;
        public ulong cFramesComplete;
        public ulong cFramesPending;
        public ulong cFramesAvailable;
        public ulong cFramesDropped;
        public ulong cFramesMissed;
        public ulong cRefreshNextDisplayed;
        public ulong cRefreshNextPresented;
        public ulong cRefreshesDisplayed;
        public ulong cRefreshesPresented;
        public ulong cRefreshStarted;
        public ulong cPixelsReceived;
        public ulong cPixelsDrawn;
        public ulong cBuffersEmpty;
    }
}

internal readonly record struct DwmFrameSample(ulong FrameCount, DateTimeOffset TimestampUtc);
