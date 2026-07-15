using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Ndjson.AsyncStreams.AspNetCore.Internals;


namespace Ndjson.AsyncStreams.AspNetCore.Http;

/// <summary>
/// A <see cref="IBindableFromHttpContext{T}"/> for async stream incoming as NDJSON or JSONL content.
/// </summary>
/// <typeparam name="T">The type of the values in the incoming async stream to be deserialized.</typeparam>
public class NdjsonAsyncEnumerableBinding<T> : IBindableFromHttpContext<NdjsonAsyncEnumerableBinding<T>>
{
    /// <summary>
    /// Gets the deserialized incoming async stream.
    /// </summary>
    public IAsyncEnumerable<T?>? Value { get; init; }

    /// <summary>
    /// Gets the a ready to use <see cref="ProblemHttpResult"/> if an issue has occured with the request.
    /// </summary>
    public ProblemHttpResult? Error { get; init; }

    /// <inheritdoc/>
    public static ValueTask<NdjsonAsyncEnumerableBinding<T>> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameter);

        if (!HasSupportedContentType(context.Request))
        {
            return ValueTask.FromResult(new NdjsonAsyncEnumerableBinding<T>
            {
                Error = TypedResults.Problem(
                    statusCode: StatusCodes.Status415UnsupportedMediaType,
                    title: "Unsupported media type",
                    detail: $"Request content type is not {MediaTypeHeaderValues.APPLICATION_NDJSON_MEDIA_TYPE} or {MediaTypeHeaderValues.APPLICATION_JSONL_MEDIA_TYPE}, or the charset is different than UTF-8."
                )
            });
        }

        return ValueTask.FromResult(new NdjsonAsyncEnumerableBinding<T>
        {
            Value = ReadFromNdjsonAsync(context)
        });
    }

    private static bool HasSupportedContentType(HttpRequest request)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaTypeHeaderValue))
        {
            return false;
        }

        if (!IsSupportedMediaType(mediaTypeHeaderValue.MediaType) || !IsUtf8Encoding(mediaTypeHeaderValue.Charset))
        {
            return false;
        }

        return true;
    }

    private static bool IsSupportedMediaType(StringSegment mediaType)
    {
        return mediaType.Equals(MediaTypeHeaderValues.APPLICATION_NDJSON_MEDIA_TYPE, StringComparison.OrdinalIgnoreCase) || mediaType.Equals(MediaTypeHeaderValues.APPLICATION_JSONL_MEDIA_TYPE, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUtf8Encoding(StringSegment charset)
    {
        try
        {
            if (charset.Length > 2 && charset[0] == '\"' && charset[charset.Length - 1] == '\"')
            {
                return Encoding.GetEncoding(charset.Substring(1, charset.Length - 2)) == Encoding.UTF8;
            }
            else if (!String.IsNullOrEmpty(charset.Value))
            {
                return Encoding.GetEncoding(charset.Value) == Encoding.UTF8;
            }
            else
            {
                return false;
            }
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException("The character set provided in ContentType is invalid.", ex);
        }
    }

    private static async IAsyncEnumerable<T?> ReadFromNdjsonAsync(HttpContext context)
    {
        JsonSerializerOptions jsonSerializerOptions = ResolveJsonOptions(context).SerializerOptions;

        using StreamReader requestBodyStreamReader = new(context.Request.Body);

        string? valueUtf8Json = await requestBodyStreamReader.ReadLineAsync();
        while (valueUtf8Json is not null)
        {
            yield return JsonSerializer.Deserialize<T>(valueUtf8Json, jsonSerializerOptions);

            valueUtf8Json = await requestBodyStreamReader.ReadLineAsync();
        }
    }

    private static JsonOptions ResolveJsonOptions(HttpContext httpContext)
    {
        return httpContext.RequestServices.GetService<IOptions<JsonOptions>>()?.Value ?? new JsonOptions();
    }
}
