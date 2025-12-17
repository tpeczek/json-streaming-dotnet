using System.Text.Encodings.Web;
using System.Text.Json;

namespace Ndjson.AsyncStreams.AspNetCore.Mvc.Internals
{
    internal static class SystemTextJsonSerializerOptionsExtensions
    {
#if NETCOREAPP3_1
        private static readonly JsonSerializerOptions _defaultJsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
#else
        private static readonly JsonSerializerOptions _defaultJsonSerializerOptions = new(JsonSerializerDefaults.Web);
#endif

        public static JsonSerializerOptions DefaultJsonSerializerOptions => _defaultJsonSerializerOptions;

        public static JsonSerializerOptions CreateCopyWithDifferentEncoder(this JsonSerializerOptions serializerOptions, JavaScriptEncoder encoder)
        {
#if NETCOREAPP3_1
            var copiedOptions = new JsonSerializerOptions
            {
                AllowTrailingCommas = serializerOptions.AllowTrailingCommas,
                DefaultBufferSize = serializerOptions.DefaultBufferSize,
                DictionaryKeyPolicy = serializerOptions.DictionaryKeyPolicy,
                IgnoreNullValues = serializerOptions.IgnoreNullValues,
                IgnoreReadOnlyProperties = serializerOptions.IgnoreReadOnlyProperties,
                MaxDepth = serializerOptions.MaxDepth,
                PropertyNameCaseInsensitive = serializerOptions.PropertyNameCaseInsensitive,
                PropertyNamingPolicy = serializerOptions.PropertyNamingPolicy,
                ReadCommentHandling = serializerOptions.ReadCommentHandling,
                WriteIndented = serializerOptions.WriteIndented
            };

            for (var i = 0; i < serializerOptions.Converters.Count; i++)
            {
                copiedOptions.Converters.Add(serializerOptions.Converters[i]);
            }
#else
            var copiedOptions = new JsonSerializerOptions(serializerOptions);
#endif
            copiedOptions.Encoder = encoder;

            return copiedOptions;
        }
    }
}
