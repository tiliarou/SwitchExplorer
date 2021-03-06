﻿using System;
using System.IO;
using LibHac.Streams;

namespace LibHac
{
    public class Package1
    {
        private const uint Pk11Magic = 0x31314B50; // PK11

        public byte[] BuildHash { get; }
        public string BuildDate { get; }
        public int Field1E { get; }
        public int Pk11Size { get; }
        public byte[] Counter { get; }
        public int KeyRevision { get; }
        public Pk11 Pk11 { get; }

        private SharedStreamSource StreamSource { get; }

        public Package1(Keyset keyset, Stream stream)
        {
            StreamSource = new SharedStreamSource(stream);
            var reader = new BinaryReader(stream);

            BuildHash = reader.ReadBytes(0x10);
            BuildDate = reader.ReadAscii(0xE);
            Field1E = reader.ReadUInt16();

            reader.BaseStream.Position = 0x3FE0;
            Pk11Size = reader.ReadInt32();

            reader.BaseStream.Position += 0xC;
            Counter = reader.ReadBytes(0x10);

            // Try decrypting the PK11 blob with all known package1 keys
            Stream encStream = StreamSource.CreateStream(0x4000, Pk11Size);
            var decBuffer = new byte[0x10];

            for (int i = 0; i < 0x20; i++)
            {
                var dec = new Aes128CtrStream(encStream, keyset.Package1Keys[i], Counter);
                dec.Read(decBuffer, 0, 0x10);

                if (BitConverter.ToUInt32(decBuffer, 0) == Pk11Magic)
                {
                    KeyRevision = i;

                    dec.Position = 0;
                    Pk11 = new Pk11(new RandomAccessSectorStream(dec));

                    return;
                }
            }

            throw new InvalidDataException("Failed to decrypt PK11! Is the correct key present?");
        }

        public Stream OpenPackage1Ldr() => StreamSource.CreateStream(0, 0x4000);
    }

    public class Pk11
    {
        private const int DataOffset = 0x20;

        public string Magic { get; }
        public int[] SectionSizes { get; } = new int[3];
        public int[] SectionOffsets { get; } = new int[3];

        private SharedStreamSource StreamSource { get; }

        public Pk11(Stream stream)
        {
            StreamSource = new SharedStreamSource(stream);
            var reader = new BinaryReader(stream);

            Magic = reader.ReadAscii(4);
            SectionSizes[0] = reader.ReadInt32();
            SectionOffsets[0] = reader.ReadInt32();

            reader.BaseStream.Position += 4;
            SectionSizes[1] = reader.ReadInt32();
            SectionOffsets[1] = reader.ReadInt32();
            SectionSizes[2] = reader.ReadInt32();
            SectionOffsets[2] = reader.ReadInt32();

            SectionOffsets[0] = DataOffset;
            SectionOffsets[1] = SectionOffsets[0] + SectionSizes[0];
            SectionOffsets[2] = SectionOffsets[1] + SectionSizes[1];
        }

        public Stream OpenSection(int index)
        {
            if (index < 0 || index > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Section index must be one of: 0, 1, 2");
            }

            return StreamSource.CreateStream(SectionOffsets[index], SectionSizes[index]);
        }

        public Stream OpenDecryptedPk11() => StreamSource.CreateStream();

        public Stream OpenWarmboot() => OpenSection(GetWarmbootSection());
        public Stream OpenNxBootloader() => OpenSection(GetNxBootloaderSection());
        public Stream OpenSecureMonitor() => OpenSection(GetSecureMonitorSection());

        // todo: Handle the old layout from before 2.0.0
        private int GetWarmbootSection() => 0;
        private int GetNxBootloaderSection() => 1;
        private int GetSecureMonitorSection() => 2;
    }
}
