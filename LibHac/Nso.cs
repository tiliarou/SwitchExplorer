﻿using System.Collections;
using System.IO;
using LibHac.Streams;

namespace LibHac
{
    public class Nso
    {
        public NsoSection[] Sections { get; }
        public RodataRelativeExtent[] RodataRelativeExtents { get; }
        public uint BssSize { get; }
        public byte[] BuildId { get; } = new byte[0x20];

        private SharedStreamSource StreamSource { get; }

        public Nso(Stream stream)
        {
            StreamSource = new SharedStreamSource(stream);
            var reader = new BinaryReader(StreamSource.CreateStream());
            if (reader.ReadAscii(4) != "NSO0")
                throw new InvalidDataException("NSO magic is incorrect!");
            reader.ReadUInt32(); // Version
            reader.ReadUInt32(); // Reserved/Unused
            var flags = new BitArray(new[] { (int)reader.ReadUInt32() });
            var textSection = new NsoSection(StreamSource);
            var rodataSection = new NsoSection(StreamSource);
            var dataSection = new NsoSection(StreamSource);
            textSection.IsCompressed = flags[0];
            rodataSection.IsCompressed = flags[1];
            dataSection.IsCompressed = flags[2];
            textSection.CheckHash = flags[3];
            rodataSection.CheckHash = flags[4];
            dataSection.CheckHash = flags[5];

            textSection.ReadSegmentHeader(reader);
            reader.ReadUInt32(); // Module offset (TODO)
            rodataSection.ReadSegmentHeader(reader);
            reader.ReadUInt32(); // Module file size
            dataSection.ReadSegmentHeader(reader);
            BssSize = reader.ReadUInt32();
            reader.Read(BuildId, 0, 0x20);
            textSection.CompressedSize = reader.ReadUInt32();
            rodataSection.CompressedSize = reader.ReadUInt32();
            dataSection.CompressedSize = reader.ReadUInt32();
            reader.ReadBytes(0x1C); // Padding
            RodataRelativeExtents = new[]
            {
                new RodataRelativeExtent(reader), new RodataRelativeExtent(reader), new RodataRelativeExtent(reader)
            };

            reader.Read(textSection.Hash, 0, 0x20);
            reader.Read(rodataSection.Hash, 0, 0x20);
            reader.Read(dataSection.Hash, 0, 0x20);

            Sections = new[] { textSection, rodataSection, dataSection };
            reader.Close();
        }

        public class NsoSection
        {
            private SharedStreamSource StreamSource { get; }

            public bool IsCompressed { get; set; }
            public bool CheckHash { get; set; }
            public uint FileOffset { get; set; }
            public uint MemoryOffset { get; set; }
            public uint DecompressedSize { get; set; }
            public uint CompressedSize { get; set; }

            public byte[] Hash { get; } = new byte[0x20];

            public NsoSection(SharedStreamSource streamSource)
            {
                StreamSource = streamSource;
            }

            public Stream OpenSection()
            {
                return StreamSource.CreateStream(FileOffset, CompressedSize);
            }

            public byte[] DecompressSection()
            {
                var compressed = new byte[CompressedSize];
                OpenSection().Read(compressed, 0, (int)CompressedSize);

                if (IsCompressed)
                    return Lz4.Decompress(compressed, (int)DecompressedSize);
                else
                    return compressed;
            }

            internal void ReadSegmentHeader(BinaryReader reader)
            {
                FileOffset = reader.ReadUInt32();
                MemoryOffset = reader.ReadUInt32();
                DecompressedSize = reader.ReadUInt32();
            }
        }

        public class RodataRelativeExtent
        {
            public uint RegionRodataOffset { get; }
            public uint RegionSize { get; }

            public RodataRelativeExtent(BinaryReader reader)
            {
                RegionRodataOffset = reader.ReadUInt32();
                RegionSize = reader.ReadUInt32();
            }
        }
    }
}
