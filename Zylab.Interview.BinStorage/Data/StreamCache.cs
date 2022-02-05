namespace Zylab.Interview.BinStorage.Data
{
    public class StreamCache
    {
        public int ReadCount { get; set; }

        public bool IsCached { get; set; }

        public byte[] DataStream { get; set; }

        public long StartingPosition { get; set; }

        public long Length { get; set; }

        public bool ToDecompress { get; set; }
    }
}
