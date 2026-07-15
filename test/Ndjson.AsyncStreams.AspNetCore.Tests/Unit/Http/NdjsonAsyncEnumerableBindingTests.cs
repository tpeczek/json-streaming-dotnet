using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Moq;
using Ndjson.AsyncStreams.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Ndjson.AsyncStreams.AspNetCore.Tests.Unit.Http
{
    public class NdjsonAsyncEnumerableBindingTests
    {
        private static readonly string NDJSON_CONTENT_TYPE = new MediaTypeHeaderValue("application/x-ndjson")
        {
            Encoding = Encoding.UTF8
        }.ToString();
        private static readonly string JSONL_CONTENT_TYPE = new MediaTypeHeaderValue("application/jsonl")
        {
            Encoding = Encoding.UTF8
        }.ToString();
        public static IEnumerable<object[]> SUPPORTED_CONTENT_TYPES => new List<object[]>
        {
            new object[] { NDJSON_CONTENT_TYPE },
            new object[] { JSONL_CONTENT_TYPE }
        };

        private struct ValueType
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private const string VALUES_CONTENT = "{\"id\":1,\"name\":\"Value 01\"}\n{\"id\":2,\"name\":\"Value 02\"}\n";
        private static readonly List<ValueType> VALUES = new()
        {
            new ValueType { Id = 1, Name = "Value 01" },
            new ValueType { Id = 2, Name = "Value 02" }
        };

        private readonly static Delegate ENDPOINT_HANDLER = (NdjsonAsyncEnumerableBinding<ValueType> binding) => { };
        private readonly static ParameterInfo ENDPOINT_HANDLER_PARAMETER = ENDPOINT_HANDLER.GetMethodInfo().GetParameters()[0];

        private static HttpContext PrepareHttpContext(string contentType, string content = VALUES_CONTENT)
        {
            HttpContext httpContext = new DefaultHttpContext
            {
                RequestServices = new Mock<IServiceProvider>().Object
            };
            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(content));
            httpContext.Request.ContentType = contentType;

            return httpContext;
        }

        [Fact]
        public async Task BindAsync_ForNotSupportedContentType_ReturnsBindingWithoutValue()
        {
            HttpContext context = PrepareHttpContext(contentType: "application/json");

            var binding = await NdjsonAsyncEnumerableBinding<ValueType>.BindAsync(context, ENDPOINT_HANDLER_PARAMETER);

            Assert.Null(binding.Value);
        }

        [Fact]
        public async Task BindAsync_ForNotSupportedContentType_ReturnsBindingWithError()
        {
            HttpContext context = PrepareHttpContext(contentType: "application/json");

            var binding = await NdjsonAsyncEnumerableBinding<ValueType>.BindAsync(context, ENDPOINT_HANDLER_PARAMETER);

            Assert.NotNull(binding.Error);
        }

        [Fact]
        public async Task BindAsync_ForNotSupportedContentType_BindingErrorStatuCodeIs415UnsupportedMediaType()
        {
            HttpContext context = PrepareHttpContext(contentType: "application/json");

            var binding = await NdjsonAsyncEnumerableBinding<ValueType>.BindAsync(context, ENDPOINT_HANDLER_PARAMETER);

            Assert.Equal(StatusCodes.Status415UnsupportedMediaType, binding.Error.StatusCode);
        }

        [Theory]
        [MemberData(nameof(SUPPORTED_CONTENT_TYPES))]
        public async Task BindAsync_ForSupportedContentType_ReturnsBindingWithoutError(string contentType)
        {
            HttpContext context = PrepareHttpContext(contentType: contentType);

            var binding = await NdjsonAsyncEnumerableBinding<ValueType>.BindAsync(context, ENDPOINT_HANDLER_PARAMETER);

            Assert.Null(binding.Error);
        }

        [Theory]
        [MemberData(nameof(SUPPORTED_CONTENT_TYPES))]
        public async Task BindAsync_ForSupportedContentType_ReturnsBindingWithValue(string contentType)
        {
            HttpContext context = PrepareHttpContext(contentType: contentType);

            var binding = await NdjsonAsyncEnumerableBinding<ValueType>.BindAsync(context, ENDPOINT_HANDLER_PARAMETER);

            Assert.NotNull(binding.Value);
        }

        [Theory]
        [MemberData(nameof(SUPPORTED_CONTENT_TYPES))]
        public async Task BindAsync_ForSupportedContentType_BindingValueContainsCorrectValues(string contentType)
        {
            HttpContext context = PrepareHttpContext(contentType: contentType);

            var binding = await NdjsonAsyncEnumerableBinding<ValueType>.BindAsync(context, ENDPOINT_HANDLER_PARAMETER);

            int valueIndex = 0;
            await foreach (ValueType value in binding.Value)
            {
                Assert.Equal(VALUES[valueIndex++], value);
            }
            Assert.Equal(VALUES.Count, valueIndex);
        }
    }
}
