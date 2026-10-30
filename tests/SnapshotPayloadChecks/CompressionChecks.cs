using System.Text;
using Designer.Modules.SnapshotInspector;

internal static class CompressionChecks
{
    internal static void Run(Action<bool, string> check)
    {
        // Fixed encoded tokens exercise all distance-bit combinations independently
        // of the production decoder. Counts cover one byte and the 31-byte maximum.
        (int Distance, int Count, byte Tag, byte Low)[] references =
        [
            (1, 31, 0x9f, 0x01), (255, 31, 0x9f, 0xff),
            (256, 31, 0xbf, 0x00), (511, 31, 0xbf, 0xff),
            (512, 31, 0xdf, 0x00), (767, 31, 0xdf, 0xff),
            (768, 31, 0xff, 0x00), (1023, 31, 0xff, 0xff),
            (1023, 1, 0xe1, 0xff)
        ];
        foreach (var (distance, count, tag, low) in references)
        {
            string seed = new(Enumerable.Range(0, distance)
                .Select(i => (char)('!' + i % 90)).ToArray());
            string expected = seed + new string(Enumerable.Range(0, count)
                .Select(i => seed[i % seed.Length]).ToArray());
            using var frame = StartFrame();
            Literals(frame, Header(1));
            Literals(frame, TextHeader('k', expected.Length));
            Literals(frame, Encoding.ASCII.GetBytes(seed));
            frame.Write([tag, low]);
            byte[] bytes = Finish(frame);
            byte[] before = (byte[])bytes.Clone();
            var parsed = SnapshotPayload.Read(bytes);
            check(parsed.Count == 1 && parsed["k"].IsText && parsed["k"].Text == expected,
                $"Reference distance {distance}, count {count} changed the decoded text.");
            check(bytes.SequenceEqual(before), "Reading a compressed payload changed its input.");
        }

        Reject([0x9f], "Truncated snapshot reference.");
        Reject([0x9f, 0x00], "Invalid snapshot reference.");
        Reject([0x9f, 0xff], "Invalid snapshot reference.");
        Reject([0x80, 0x01], "Invalid snapshot expansion.");
        Reject([0x02, 0x69], "Truncated snapshot literal.");

        // Two text records fill exactly the existing 32 MiB expansion cap.
        // Each string stays within the separate 16 MiB string-length bound.
        // Encoding repeated bytes keeps the input small; no native fixture is used.
        const int expandedLimit = 32 * 1024 * 1024;
        const int firstLength = 16 * 1024 * 1024;
        int overhead = Header(2).Length + TextHeader('a', 0).Length + TextHeader('b', 0).Length;
        int secondLength = expandedLimit - overhead - firstLength;
        byte[] boundary = ExpandedFrame(firstLength, secondLength);
        var values = SnapshotPayload.Read(boundary);
        check(values.Count == 2
            && values["a"].Text is { } first && first.Length == firstLength && first.All(c => c == 'A')
            && values["b"].Text is { } second && second.Length == secondLength && second.All(c => c == 'B'),
            "A valid payload at the expansion cap must decode every text byte.");
        RejectFrame(ExpandedFrame(firstLength, secondLength + 1), "Invalid snapshot expansion.");

        void Reject(byte[] tail, string expectedMessage)
        {
            using var frame = StartFrame();
            Literals(frame, Header(0));
            frame.Write(tail);
            RejectFrame(Finish(frame), expectedMessage);
        }

        void RejectFrame(byte[] bytes, string expectedMessage)
        {
            byte[] before = (byte[])bytes.Clone();
            try
            {
                SnapshotPayload.Read(bytes);
            }
            catch (InvalidDataException error)
            {
                check(error.Message == expectedMessage,
                    $"Expected '{expectedMessage}', received '{error.Message}'.");
                check(bytes.SequenceEqual(before), "Rejecting a compressed payload changed its input.");
                return;
            }
            throw new Exception($"Malformed compressed payload was accepted; expected '{expectedMessage}'.");
        }
    }

    private static MemoryStream StartFrame()
    {
        var frame = new MemoryStream();
        frame.WriteByte((byte)'q');
        return frame;
    }

    private static byte[] Finish(MemoryStream frame)
    {
        // This trailer meets the observed shape; it does not assert a checksum.
        frame.Write(Encoding.ASCII.GetBytes(new string('0', 32)));
        return frame.ToArray();
    }

    private static void Literals(MemoryStream frame, byte[] bytes)
    {
        for (int offset = 0; offset < bytes.Length; offset += 127)
        {
            int count = Math.Min(127, bytes.Length - offset);
            frame.WriteByte((byte)count);
            frame.Write(bytes, offset, count);
        }
    }

    private static byte[] Header(int records)
    {
        using var bytes = new MemoryStream();
        using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
        writer.Write((byte)'i');
        writer.Write(0x0bf7fbd9);
        writer.Write((byte)'i');
        writer.Write(records);
        return bytes.ToArray();
    }

    private static byte[] TextHeader(char key, int length)
    {
        using var bytes = new MemoryStream();
        using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
        writer.Write((byte)'s');
        writer.Write(1);
        writer.Write((byte)key);
        writer.Write((byte)'i');
        writer.Write(4);
        writer.Write((byte)'s');
        writer.Write(length);
        return bytes.ToArray();
    }

    private static byte[] ExpandedFrame(int firstLength, int secondLength)
    {
        using var frame = StartFrame();
        Literals(frame, Header(2));
        Text('a', 'A', firstLength);
        Text('b', 'B', secondLength);
        return Finish(frame);

        void Text(char key, char value, int length)
        {
            Literals(frame, TextHeader(key, length));
            Literals(frame, [(byte)value]);
            for (int remaining = length - 1; remaining > 0;)
            {
                int count = Math.Min(31, remaining);
                frame.WriteByte((byte)(0x80 | count));
                frame.WriteByte(1);
                remaining -= count;
            }
        }
    }
}
