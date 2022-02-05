using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System.IO;
using Zylab.Interview.BinStorage.Dto;

namespace Zylab.Interview.BinStorage.Serializers
{
    public static class IndexSerializer
    {
        //Lets imagine it is great serializer
        //May be just string interpolation will be better
        public static byte[] Serilalize(IndexDto indexDto)
        {
            byte[] result;
            MemoryStream ms = new MemoryStream();
            using (var writer = new BsonWriter(ms))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(writer, indexDto);
            }
            return ms.ToArray();
        }

        public static IndexDto IndexDeserialize(byte[] data)
        {
            MemoryStream ms = new MemoryStream(data);
            using (var reader = new BsonReader(ms))
            {
                var serializer = new JsonSerializer();
                return serializer.Deserialize<IndexDto>(reader);
            }
        }
    }
}
