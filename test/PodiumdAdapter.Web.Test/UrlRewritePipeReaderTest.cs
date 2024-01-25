using System.IO.Pipelines;
using System.Text;
using PodiumdAdapter.Web.Infrastructure.UrlRewriter;

namespace PodiumdAdapter.Web.Test
{
    public class UrlRewritePipeReaderTest
    {
        [Fact]
        public async Task BasicTest()
        {
            var (reader, writer) = CreatePipe("replace-", "me", "with-", "this");

            await WriteAsync(writer, "start-");
            await WriteAsync(writer, "before-replace-me-after");

            var readTask = Read(reader);

            await WriteAsync(writer, "-end");
            await writer.CompleteAsync();

            var output = await readTask;
            await reader.DisposeAsync();

            Assert.Equal("start-before-with-this-after-end", output);
        }

        private static (Stream Reader, PipeWriter Writer) CreatePipe(string localRoot, string localPath, string remoteRoot, string remotePath)
        {
            var pipe = new Pipe();
            var writer = pipe.Writer;
            var replacer = new UrlRewriter(localRoot + localPath, remoteRoot + remotePath);
            var reader = new UrlRewriteReadStream(pipe.Reader.AsStream(), new(localRoot, remoteRoot, [replacer]));
            return (reader, writer);
        }

        private static async Task WriteAsync(PipeWriter writer, string str) => await writer.WriteAsync(Encoding.UTF8.GetBytes(str));

        private static async Task<string> Read(Stream stream)
        {
            using var memory = new MemoryStream();
            await StreamCopier.CopyAsync(stream, memory, -1, default);
            memory.Seek(0, SeekOrigin.Begin);
            using var strReader = new StreamReader(memory);
            return await strReader.ReadToEndAsync();
        }
    }
}
