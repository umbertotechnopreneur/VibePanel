// VBWR B
// Project: ThermalrightVibeWare | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Buffers.Binary;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;

namespace ThermalrightVibeWare.Core;

/// <summary>
/// Opt-in, in-memory analysis of the default Windows render endpoint. It never opens an input
/// endpoint, writes audio to disk, or sends audio over a network. Constructing it opens nothing.
/// </summary>
public sealed class WindowsPlaybackAnalyzer : IPlaybackProvider
{
    private const int TransformSize = 4096;
    private const int BandCount = 32;
    private const double FloorDbfs = -90;
    private readonly object gate = new();
    private CaptureSession? session;
    private long nextDeviceCheck;
    private bool disposed;
    private PlaybackAnalysis unavailable = Unavailable("Playback analysis is off.");

    /// <summary>Starts lazily and checks the default output or retries a failure at most once a second.</summary>
    public PlaybackAnalysis Read()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!OperatingSystem.IsWindows())
            {
                unavailable = Unavailable("Playback analysis requires the Windows backend.");
                return Copy(unavailable);
            }
            var now = Environment.TickCount64;
            if (now >= nextDeviceCheck)
            {
                nextDeviceCheck = now + 1000;
                MMDevice? candidate = null;
                try
                {
                    using var enumerator = new MMDeviceEnumerator();
                    candidate = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    if (candidate.DataFlow != DataFlow.Render) { throw new InvalidOperationException("No playback endpoint is available."); }
                    if (session is null || session.Stopped || !StringComparer.Ordinal.Equals(candidate.ID, session.DeviceId))
                    {
                        CloseSession();
                        var selected = candidate;
                        candidate = null; // CaptureSession owns and disposes the selected endpoint, including on failure.
                        session = new CaptureSession(selected);
                    }
                }
                catch (Exception error)
                {
                    CloseSession();
                    unavailable = Unavailable($"Playback analysis unavailable: {error.Message}");
                }
                finally { candidate?.Dispose(); }
            }
            return SnapshotCore();
        }
    }

    /// <summary>Reads only the latest bounded analysis; does not open or enumerate devices.</summary>
    public PlaybackAnalysis Snapshot()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return SnapshotCore();
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            if (session is null && nextDeviceCheck == 0) { return; }
            CloseSession();
            nextDeviceCheck = 0;
            unavailable = Unavailable("Playback analysis is off.");
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) { return; }
            disposed = true;
            CloseSession();
        }
    }

    private PlaybackAnalysis SnapshotCore() => session?.Snapshot() ?? Copy(unavailable);

    private void CloseSession()
    {
        var previous = session;
        session = null;
        previous?.Dispose();
    }

    private static PlaybackAnalysis Copy(PlaybackAnalysis value) => value with
    {
        BandDbfs = (double[])value.BandDbfs.Clone(),
        FrequenciesHz = (double[])value.FrequenciesHz.Clone()
    };

    private static double[] FloorBands()
    {
        var values = new double[BandCount];
        Array.Fill(values, FloorDbfs);
        return values;
    }

    private static double[] BandCenters(int sampleRate)
    {
        var upper = sampleRate > 0 ? Math.Min(20000d, sampleRate / 2d) : 20000d;
        var centers = new double[BandCount];
        for (var band = 0; band < BandCount; band++)
        { centers[band] = 20 * Math.Pow(upper / 20, band / (double)(BandCount - 1)); }
        return centers;
    }

    private static PlaybackAnalysis Unavailable(string status) => new(false, "system-output", 0, TransformSize, 0,
        FloorBands(), BandCenters(0), null, null, null, null, null, null, null, status);

    // WasapiLoopbackCapture's standard constructor requests 100 ms. This documented extension
    // point requests a 50 ms shared-mode buffer, but the endpoint chooses its actual latency.
    private sealed class OutputCapture(MMDevice device) : WasapiCapture(device, false, 50)
    {
        protected override AudioClientStreamFlags GetAudioClientStreamFlags() =>
            base.GetAudioClientStreamFlags() | AudioClientStreamFlags.Loopback;
    }

    private sealed class CaptureSession : IDisposable
    {
        private static readonly Guid PcmSubtype = new("00000001-0000-0010-8000-00aa00389b71");
        private static readonly Guid FloatSubtype = new("00000003-0000-0010-8000-00aa00389b71");
        private readonly object dataGate = new();
        private readonly MMDevice device;
        private readonly OutputCapture capture;
        private readonly int sampleRate;
        private readonly int channels;
        private readonly int sampleBytes;
        private readonly int blockAlign;
        private readonly bool floatingPoint;
        private readonly float[][] ring;
        private readonly float[] window = new float[TransformSize];
        private readonly Complex[] transform = new Complex[TransformSize];
        private readonly double[] powers = new double[TransformSize / 2 + 1];
        private readonly double[] frequencies;
        private readonly int[] firstBin = new int[BandCount];
        private readonly int[] lastBin = new int[BandCount];
        private readonly double fftGain;
        private readonly long startedAt = Environment.TickCount64;
        private PlaybackAnalysis latest;
        private int writeIndex;
        private int filledFrames;
        private int meterFrames;
        private double leftSquares;
        private double rightSquares;
        private double leftPeak;
        private double rightPeak;
        private double leftHold = FloorDbfs;
        private double rightHold = FloorDbfs;
        private long leftHeldAt;
        private long rightHeldAt;
        private long lastPublished;
        private long lastData;
        private bool disposed;
        private volatile bool stopped;

        public string DeviceId { get; }
        public bool Stopped => stopped;

        public CaptureSession(MMDevice selected)
        {
            device = selected;
            OutputCapture? created = null;
            try
            {
                DeviceId = selected.ID;
                created = new OutputCapture(selected);
                var format = created.WaveFormat;
                sampleRate = format.SampleRate;
                channels = format.Channels;
                sampleBytes = format.BitsPerSample / 8;
                blockAlign = format.BlockAlign;
                var encoding = format.Encoding;
                if (encoding == WaveFormatEncoding.Extensible)
                {
                    if (format is not WaveFormatExtensible extended) { throw new NotSupportedException("Unknown extensible audio format."); }
                    encoding = extended.SubFormat == FloatSubtype ? WaveFormatEncoding.IeeeFloat
                        : extended.SubFormat == PcmSubtype ? WaveFormatEncoding.Pcm
                        : throw new NotSupportedException("Unsupported extensible audio subtype.");
                }
                floatingPoint = encoding == WaveFormatEncoding.IeeeFloat;
                if (sampleRate is < 8000 or > 192000 || channels is < 1 or > 8 || blockAlign != channels * sampleBytes ||
                    (floatingPoint ? format.BitsPerSample != 32 : encoding != WaveFormatEncoding.Pcm || format.BitsPerSample is not (16 or 24 or 32)))
                { throw new NotSupportedException("Playback analysis supports float32 or PCM16/24/32, 1–8 channels, 8–192 kHz."); }

                ring = new float[channels][];
                for (var channel = 0; channel < channels; channel++) { ring[channel] = new float[TransformSize]; }
                double windowSum = 0;
                for (var index = 0; index < TransformSize; index++)
                {
                    window[index] = (float)(0.5 * (1 - Math.Cos(2 * Math.PI * index / (TransformSize - 1))));
                    windowSum += window[index];
                }
                // NAudio's forward FFT already divides by N. Correct the Hann coherent gain.
                fftGain = TransformSize / windowSum;
                frequencies = BandCenters(sampleRate);
                for (var band = 0; band < BandCount; band++)
                {
                    var low = band == 0 ? 20 : Math.Sqrt(frequencies[band - 1] * frequencies[band]);
                    var high = band == BandCount - 1 ? Math.Min(20000d, sampleRate / 2d) : Math.Sqrt(frequencies[band] * frequencies[band + 1]);
                    firstBin[band] = Math.Clamp((int)Math.Ceiling(low * TransformSize / sampleRate), 1, TransformSize / 2);
                    lastBin[band] = Math.Clamp((int)Math.Floor(high * TransformSize / sampleRate), 1, TransformSize / 2);
                    if (lastBin[band] < firstBin[band])
                    {
                        // Low bands narrower than an FFT bin share the nearest measured bin.
                        firstBin[band] = lastBin[band] = Math.Clamp((int)Math.Round(frequencies[band] * TransformSize / sampleRate), 1, TransformSize / 2);
                    }
                }
                latest = new(false, "system-output", sampleRate, TransformSize, channels, FloorBands(), frequencies,
                    null, null, null, null, null, null, null, "Waiting for system output.");
                capture = created;
                capture.DataAvailable += OnData;
                capture.RecordingStopped += OnStopped;
                capture.StartRecording();
            }
            catch
            {
                try { created?.Dispose(); }
                finally { selected.Dispose(); }
                throw;
            }
        }

        public PlaybackAnalysis Snapshot()
        {
            lock (dataGate)
            {
                if (!stopped && Environment.TickCount64 - (lastData > 0 ? lastData : startedAt) > 150)
                {
                    return latest with
                    {
                        Available = true,
                        BandDbfs = FloorBands(),
                        FrequenciesHz = (double[])frequencies.Clone(),
                        LeftPeakDbfs = FloorDbfs,
                        RightPeakDbfs = channels > 1 ? FloorDbfs : null,
                        LeftRmsDbfs = FloorDbfs,
                        RightRmsDbfs = channels > 1 ? FloorDbfs : null,
                        LeftPeakHoldDbfs = FloorDbfs,
                        RightPeakHoldDbfs = channels > 1 ? FloorDbfs : null,
                        Status = "No recent audio data."
                    };
                }
                return Copy(latest);
            }
        }

        private void OnData(object? sender, WaveInEventArgs args)
        {
            lock (dataGate)
            {
                if (disposed || stopped || args.BytesRecorded <= 0) { return; }
                try
                {
                    var now = Environment.TickCount64;
                    if (lastData > 0 && now - lastData > 150) { ResetSamples(); }
                    lastData = now;
                    var bytes = Math.Min(args.BytesRecorded, args.Buffer.Length);
                    var frames = bytes / blockAlign;
                    // Bound callback work even if a driver supplies an unexpectedly large packet.
                    var firstFrame = Math.Max(0, frames - sampleRate / 5);
                    if (firstFrame > 0) { ResetSamples(); }
                    for (var frame = firstFrame; frame < frames; frame++)
                    {
                        var offset = frame * blockAlign;
                        for (var channel = 0; channel < channels; channel++)
                        {
                            var sample = Decode(args.Buffer.AsSpan(offset + channel * sampleBytes, sampleBytes));
                            ring[channel][writeIndex] = sample;
                            if (channel == 0) { leftSquares += sample * (double)sample; leftPeak = Math.Max(leftPeak, Math.Abs(sample)); }
                            else if (channel == 1) { rightSquares += sample * (double)sample; rightPeak = Math.Max(rightPeak, Math.Abs(sample)); }
                        }
                        writeIndex = (writeIndex + 1) % TransformSize;
                        filledFrames = Math.Min(TransformSize, filledFrames + 1);
                        meterFrames++;
                    }
                    // At most one transform per callback and at most 20 transforms a second.
                    if (filledFrames == TransformSize && meterFrames >= sampleRate / 20 && now - lastPublished >= 50)
                    { Publish(now); }
                }
                catch (Exception error)
                {
                    stopped = true;
                    latest = Unavailable($"Playback analysis unavailable: {error.Message}");
                }
            }
        }

        private float Decode(ReadOnlySpan<byte> value)
        {
            if (floatingPoint)
            {
                var sample = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(value));
                return float.IsFinite(sample) ? Math.Clamp(sample, -1f, 1f) : 0;
            }
            return sampleBytes switch
            {
                2 => BinaryPrimitives.ReadInt16LittleEndian(value) / 32768f,
                3 => ((value[0] | value[1] << 8 | value[2] << 16) << 8 >> 8) / 8388608f,
                // Valid 24-in-32 extensible PCM is left aligned in its signed 32-bit container.
                4 => (float)(BinaryPrimitives.ReadInt32LittleEndian(value) / 2147483648d),
                _ => 0
            };
        }

        private void Publish(long now)
        {
            Array.Clear(powers);
            for (var channel = 0; channel < channels; channel++)
            {
                for (var index = 0; index < TransformSize; index++)
                {
                    transform[index].X = ring[channel][(writeIndex + index) % TransformSize] * window[index];
                    transform[index].Y = 0;
                }
                FastFourierTransform.FFT(true, 12, transform);
                for (var bin = 1; bin < powers.Length; bin++)
                {
                    var gain = bin == TransformSize / 2 ? fftGain : 2 * fftGain;
                    var real = (double)transform[bin].X;
                    var imaginary = (double)transform[bin].Y;
                    powers[bin] += (real * real + imaginary * imaginary) * gain * gain / channels;
                }
            }
            var bands = new double[BandCount];
            for (var band = 0; band < BandCount; band++)
            {
                double peakPower = 0;
                for (var bin = firstBin[band]; bin <= lastBin[band]; bin++) { peakPower = Math.Max(peakPower, powers[bin]); }
                // The bar is the strongest measured FFT bin in the band, not invented interpolation.
                bands[band] = PowerDbfs(peakPower);
            }
            var leftDbfs = AmplitudeDbfs(leftPeak);
            var rightDbfs = AmplitudeDbfs(rightPeak);
            UpdateHold(leftDbfs, now, ref leftHold, ref leftHeldAt);
            UpdateHold(rightDbfs, now, ref rightHold, ref rightHeldAt);
            latest = new(true, "system-output", sampleRate, TransformSize, channels, bands, frequencies,
                leftDbfs, channels > 1 ? rightDbfs : null,
                PowerDbfs(leftSquares / meterFrames), channels > 1 ? PowerDbfs(rightSquares / meterFrames) : null,
                leftHold, channels > 1 ? rightHold : null, DateTimeOffset.UtcNow,
                channels > 2 ? $"System output · {channels} channels · front L/R meters" : "System output");
            lastPublished = now;
            ResetMeters();
        }

        private void UpdateHold(double peakDbfs, long now, ref double heldDbfs, ref long heldAt)
        {
            if (heldAt == 0 || peakDbfs >= heldDbfs) { heldDbfs = peakDbfs; heldAt = now; }
            else
            {
                var releaseSeconds = Math.Max(0, now - Math.Max(lastPublished, heldAt + 1000)) / 1000d;
                heldDbfs = Math.Max(peakDbfs, Math.Max(FloorDbfs, heldDbfs - 12 * releaseSeconds));
            }
        }

        private static double AmplitudeDbfs(double value) => value > 0 ? Math.Clamp(20 * Math.Log10(value), FloorDbfs, 0) : FloorDbfs;
        private static double PowerDbfs(double value) => value > 0 ? Math.Clamp(10 * Math.Log10(value), FloorDbfs, 0) : FloorDbfs;

        private void ResetSamples()
        {
            // A discontinuity must not reuse old samples as new signal.
            filledFrames = 0;
            writeIndex = 0;
            leftHold = rightHold = FloorDbfs;
            leftHeldAt = rightHeldAt = 0;
            latest = latest with
            {
                Available = false,
                BandDbfs = FloorBands(),
                LeftPeakDbfs = null,
                RightPeakDbfs = null,
                LeftRmsDbfs = null,
                RightRmsDbfs = null,
                LeftPeakHoldDbfs = null,
                RightPeakHoldDbfs = null,
                Status = "Collecting system output."
            };
            ResetMeters();
        }

        private void ResetMeters()
        {
            meterFrames = 0;
            leftSquares = rightSquares = leftPeak = rightPeak = 0;
        }

        private void OnStopped(object? sender, StoppedEventArgs args)
        {
            lock (dataGate)
            {
                if (disposed) { return; }
                stopped = true;
                latest = Unavailable(args.Exception is { } error ? $"Playback analysis stopped: {error.Message}" : "Playback analysis stopped.");
            }
            // Never dispose here: NAudio can raise this event on the very thread Dispose joins.
        }

        public void Dispose()
        {
            lock (dataGate)
            {
                if (disposed) { return; }
                disposed = true;
            }
            // Release dataGate before joining the capture thread, whose callbacks also take it.
            capture.DataAvailable -= OnData;
            capture.RecordingStopped -= OnStopped;
            try { capture.Dispose(); }
            finally { device.Dispose(); }
        }
    }
}
