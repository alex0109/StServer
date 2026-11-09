using System.Buffers.Binary;

namespace Application.Questions;

// A random sequence that is the same every time for the same pair of ids: an order picked once
// for a run (an attempt, a review session) without storing it
public static class StableRandom
{
    public static Random For(Guid first, Guid second) => new(Seed(first, second));

    // Built from the bytes themselves: Guid.GetHashCode and HashCode are not promised to stay the same
    private static int Seed(Guid first, Guid second)
    {
        Span<byte> a = stackalloc byte[16];
        Span<byte> b = stackalloc byte[16];
        first.TryWriteBytes(a);
        second.TryWriteBytes(b);

        var seed = 0;
        for (var i = 0; i < 16; i += 4)
            seed = unchecked(seed * 31 + (BinaryPrimitives.ReadInt32LittleEndian(a[i..]) ^ BinaryPrimitives.ReadInt32LittleEndian(b[i..])));

        return seed;
    }
}
