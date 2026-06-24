using FinanceManager.Domain.Identity.Entities;
using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class RequestSizeLimitTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    [Fact]
    public async Task BodylessHealthRequest_WithoutContentLength_IsNotRejected()
    {
        var response = await Client.GetAsync("/alive", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ImportEndpoint_WithOversizedPayload_ReturnsRequestEntityTooLarge()
    {
        Authorize("user", 1, UserRole.User);
        const long contentLength = 100L * 1024 * 1024;
        using var content = new StreamContent(new RepeatingByteStream(contentLength));
        content.Headers.ContentLength = contentLength;
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var response = await Client.PostAsync("api/BondAccountImport/ImportBondEntries", content, TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode)413, response.StatusCode);
    }

    private sealed class RepeatingByteStream(long length) : Stream
    {
        private long _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= length)
                return 0;

            var bytesToRead = (int)Math.Min(count, length - _position);
            Array.Fill(buffer, (byte)' ', offset, bytesToRead);
            _position += bytesToRead;
            return bytesToRead;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_position >= length)
                return ValueTask.FromResult(0);

            var bytesToRead = (int)Math.Min(buffer.Length, length - _position);
            buffer[..bytesToRead].Span.Fill((byte)' ');
            _position += bytesToRead;
            return ValueTask.FromResult(bytesToRead);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}