using System.IO;

namespace Zylab.Interview.BinStorage
{
    public enum SaveResult
    {
         Ok,
         SaveError,
         SizeExpired
    }

    public class SaveResultWithData
    {
        public SaveResult SaveResult { get; set; }

        public long Length { get; set; }

        public long StartingPosition { get; set; }
    }

    public class ReturnResultWithStream
    {
        public bool IsOk { get; set; }
        public byte [] DataStream { get; set; }
    }
}
