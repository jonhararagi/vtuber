using System.Buffers.Binary;

namespace VtuberBot.Core;

/// <summary>Converts little-endian signed PCM16 microphone frames to a bounded input level.</summary>
public static class Pcm16LevelPolicy
{
    public static double CalculatePercent(ReadOnlySpan<byte> pcm16, double gain = 450)
    {
        if (!double.IsFinite(gain) || gain < 0)
            throw new ArgumentOutOfRangeException(nameof(gain), "La ganancia debe ser finita y no negativa.");

        var sampleCount = pcm16.Length / sizeof(short);
        if (sampleCount == 0 || gain == 0)
            return 0;

        double sumSquares = 0;
        for (var offset = 0; offset + 1 < pcm16.Length; offset += sizeof(short))
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(pcm16.Slice(offset, sizeof(short)));
            sumSquares += (double)sample * sample;
        }

        var rms = Math.Sqrt(sumSquares / sampleCount) / 32768d;
        return Math.Clamp(rms * gain, 0d, 100d);
    }
}
