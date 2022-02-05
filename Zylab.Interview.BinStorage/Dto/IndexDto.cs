namespace Zylab.Interview.BinStorage.Dto
{
    public class IndexDto
    {
        public string Key { get; set; }
        public byte[] HashforDataChangeCheck { get; set; }
        public long Length { get; set; }
        public long StartingPosition { get; set; }
    }
}
