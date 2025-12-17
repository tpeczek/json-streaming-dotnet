using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Ndjson.AsyncStreams.AspNetCore.Mvc.Formatters;
using Ndjson.AsyncStreams.AspNetCore.Mvc.NewtonsoftJson.Formatters;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace Ndjson.AsyncStreams.AspNetCore.Mvc.Tests.Unit.Formatters
{
    public class NdjsonOutputFormatterTests
    {
        private struct ValueType
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private const string NDJSON_MEDIA_TYPE = "application/x-ndjson";
        private const string JSONL_MEDIA_TYPE = "application/jsonl";

        private const string VALUES = "{\"id\":1,\"name\":\"Value 01\"}\n{\"id\":2,\"name\":\"Value 02\"}\n";

        public static IEnumerable<object[]> OutputFormatters => new List<object[]>
        {
            new object[] { PrepareSystemTextNdjsonOutputFormatter() },
            new object[] { PrepareNewtonsoftNdjsonOutputFormatter() }
        };

        public static IEnumerable<object[]> OutputFormattersAndMediaTypesMatrix => new List<object[]>
        {
            new object[] { PrepareSystemTextNdjsonOutputFormatter(), NDJSON_MEDIA_TYPE },
            new object[] { PrepareSystemTextNdjsonOutputFormatter(), JSONL_MEDIA_TYPE },
            new object[] { PrepareNewtonsoftNdjsonOutputFormatter(), NDJSON_MEDIA_TYPE },
            new object[] { PrepareNewtonsoftNdjsonOutputFormatter(), JSONL_MEDIA_TYPE }
        };

        private static TextOutputFormatter PrepareSystemTextNdjsonOutputFormatter()
        {
            return new SystemTextNdjsonOutputFormatter(new JsonOptions(), new NullLogger<SystemTextNdjsonOutputFormatter>());
        }

        private static TextOutputFormatter PrepareNewtonsoftNdjsonOutputFormatter()
        {
            return new NewtonsoftNdjsonOutputFormatter(
                new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() },
                ArrayPool<char>.Create(),
                new NullLogger<NewtonsoftNdjsonOutputFormatter>()
            );
        }

        private static async IAsyncEnumerable<ValueType> GetValuesAsync()
        {
            await Task.CompletedTask;

            yield return new ValueType { Id = 1, Name = "Value 01" };

            await Task.CompletedTask;

            yield return new ValueType { Id = 2, Name = "Value 02" };
        }

        private static TextWriter CreateWriterForOutputFormatterWriteContext(Stream stream, Encoding encoding)
        {
            return new HttpResponseStreamWriter(stream, encoding, 16 * 1024);
        }

        private static OutputFormatterWriteContext PrepareOutputFormatterCanWriteContext(string contentType, object @object = null)
        {
            HttpContext httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.Append("Accept", contentType);
            httpContext.Response.Body = new MemoryStream();

            return new OutputFormatterWriteContext(
                httpContext,
                CreateWriterForOutputFormatterWriteContext,
                @object is null ? typeof(IAsyncEnumerable<ValueType>) : @object.GetType(),
                @object ?? GetValuesAsync()
            )
            { ContentType = new StringSegment(httpContext.Request.Headers["Accept"]) };
        }

        [Theory]
        [MemberData(nameof(OutputFormatters))]
        public void SupportedMediaTypes_ContainsNdjsonAndJsonl(TextOutputFormatter outputFormatter)
        {
            Assert.Collection(outputFormatter.SupportedMediaTypes,
                (string mediaType) => Assert.Equal(NDJSON_MEDIA_TYPE, mediaType),
                (string mediaType) => Assert.Equal(JSONL_MEDIA_TYPE, mediaType)
            );
        }

        [Theory]
        [MemberData(nameof(OutputFormatters))]
        public void SupportedEncodings_ContainsOnlyUTF8Encoding(TextOutputFormatter outputFormatter)
        {
            Assert.Collection(outputFormatter.SupportedEncodings, (Encoding encoding) => Assert.Equal(Encoding.UTF8, encoding));
        }

        [Theory]
        [MemberData(nameof(OutputFormattersAndMediaTypesMatrix))]
        public void CanWriteResult_ForSupportedContentTypeAndObjectType_ReturnsTrue(TextOutputFormatter outputFormatter, string contentType)
        {
            OutputFormatterWriteContext outputFormatterCanWriteContext = PrepareOutputFormatterCanWriteContext(contentType);

            bool canWriteResult = outputFormatter.CanWriteResult(outputFormatterCanWriteContext);

            Assert.True(canWriteResult);
        }

        [Theory]
        [MemberData(nameof(OutputFormatters))]
        public void CanWriteResult_ForNotSupportedContentTypeAndSupportedObjectType_ReturnsFalse(TextOutputFormatter outputFormatter)
        {
            OutputFormatterWriteContext outputFormatterCanWriteContext = PrepareOutputFormatterCanWriteContext(contentType: "application/json");

            bool canWriteResult = outputFormatter.CanWriteResult(outputFormatterCanWriteContext);

            Assert.False(canWriteResult);
        }

        [Theory]
        [MemberData(nameof(OutputFormattersAndMediaTypesMatrix))]
        public void CanWriteResult_SupportedContentTypeAndNotSupportedObjectType_ReturnsTrue(TextOutputFormatter outputFormatter, string contentType)
        {
            OutputFormatterWriteContext outputFormatterCanWriteContext = PrepareOutputFormatterCanWriteContext(contentType, @object: Enumerable.Empty<ValueType>());

            bool canWriteResult = outputFormatter.CanWriteResult(outputFormatterCanWriteContext);

            Assert.False(canWriteResult);
        }

        [Theory]
        [MemberData(nameof(OutputFormattersAndMediaTypesMatrix))]
        public async Task WriteAsync_ForSupportedContentTypeAndObjectType_WritesCorrectValues(TextOutputFormatter outputFormatter, string contentType)
        {
            OutputFormatterWriteContext outputFormatterCanWriteContext = PrepareOutputFormatterCanWriteContext(contentType);

            await outputFormatter.WriteAsync(outputFormatterCanWriteContext);

            using StreamReader responseBodyReader = new StreamReader(outputFormatterCanWriteContext.HttpContext.Response.Body);
            outputFormatterCanWriteContext.HttpContext.Response.Body.Seek(0, SeekOrigin.Begin);

            Assert.Equal(VALUES, await responseBodyReader.ReadToEndAsync());
        }
    }
}
