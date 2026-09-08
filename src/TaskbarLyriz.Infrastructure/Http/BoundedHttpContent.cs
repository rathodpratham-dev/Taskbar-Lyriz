namespace TaskbarLyriz.Infrastructure.Http;

internal static class BoundedHttpContent
{
    internal static async Task<byte[]> ReadBytesAsync(
        HttpContent content,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Headers.ContentLength is > 0 and var contentLength && contentLength > maximumBytes)
        {
            throw new HttpRequestException($"The response exceeded the {maximumBytes}-byte limit.");
        }

        await using var source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var destination = new MemoryStream();
        var buffer = new byte[16_384];
        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                break;
            }

            if (destination.Length + bytesRead > maximumBytes)
            {
                throw new HttpRequestException($"The response exceeded the {maximumBytes}-byte limit.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
        }

        return destination.ToArray();
    }
}
