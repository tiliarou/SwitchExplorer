﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibHac.Streams;

namespace LibHac
{
    public class Xci
    {
        private const string RootPartitionName = "rootpt";
        private const string UpdatePartitionName = "update";
        private const string NormalPartitionName = "normal";
        private const string SecurePartitionName = "secure";
        private const string LogoPartitionName = "logo";

        public XciHeader Header { get; }

        public XciPartition RootPartition { get; }
        public XciPartition UpdatePartition { get; }
        public XciPartition NormalPartition { get; }
        public XciPartition SecurePartition { get; }
        public XciPartition LogoPartition { get; }

        public List<XciPartition> Partitions { get; } = new List<XciPartition>();

        public Xci(Keyset keyset, Stream stream)
        {
            Header = new XciHeader(keyset, stream);
            var hfs0Stream = new SubStream(stream, Header.PartitionFsHeaderAddress);

            RootPartition = new XciPartition(hfs0Stream)
            {
                Name = RootPartitionName,
                Offset = Header.PartitionFsHeaderAddress,
                HashValidity = Header.PartitionFsHeaderValidity 
            };

            Partitions.Add(RootPartition);

            foreach (PfsFileEntry file in RootPartition.Files)
            {
                Stream partitionStream = RootPartition.OpenFile(file);

                var partition = new XciPartition(partitionStream)
                {
                    Name = file.Name,
                    Offset = Header.PartitionFsHeaderAddress + RootPartition.HeaderSize + file.Offset,
                   HashValidity = file.HashValidity
                };

                Partitions.Add(partition);
            }

            UpdatePartition = Partitions.FirstOrDefault(x => x.Name == UpdatePartitionName);
            NormalPartition = Partitions.FirstOrDefault(x => x.Name == NormalPartitionName);
            SecurePartition = Partitions.FirstOrDefault(x => x.Name == SecurePartitionName);
            LogoPartition = Partitions.FirstOrDefault(x => x.Name == LogoPartitionName);
        }
    }

    public class XciPartition : Pfs
    {
        public string Name { get; internal set; }
        public long Offset { get; internal set; }
        public Validity HashValidity { get; set; } = Validity.Unchecked;

        public XciPartition(Stream stream) : base(stream) { }
    }
}
