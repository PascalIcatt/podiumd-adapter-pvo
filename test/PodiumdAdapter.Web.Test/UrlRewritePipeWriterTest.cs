using System.IO.Pipelines;
using System.Text;
using PodiumdAdapter.Web.Infrastructure.UrlRewriter;

namespace PodiumdAdapter.Web.Test
{
    public class UrlRewritePipeWriterTest
    {
        [Fact]
        public async Task When_the_full_url_fits_within_one_buffer()
        {
            var output = await UsingPipe("with-", "this", "replace-", "me", async write =>
            {
                await write("start-");
                await write("replace-me");
                await write("-end");
            });

            Assert.Equal("start-with-this-end", output);
        }

        [Fact]
        public async Task When_the_base_url_to_replace_is_split_between_buffers()
        {
            var output = await UsingPipe("with-", "this", "replace-", "me", async write =>
            {
                await write("start-");
                await write("before-repla");
                await write("ce-me-after");
                await write("-end");
            });

            Assert.Equal("start-before-with-this-after-end", output);
        }

        [Fact]
        public async Task When_the_relative_url_to_replace_is_split_between_buffers()
        {
            var output = await UsingPipe("with-", "this", "replace-", "me", async write =>
            {
                await write("start-");
                await write("before-replace-m");
                await write("e-after");
                await write("-end");
            });

            Assert.Equal("start-before-with-this-after-end", output);
        }

        [Fact]
        public async Task When_the_base_url_is_seperated_from_the_relative_url_between_buffers()
        {
            var output = await UsingPipe("with-", "this", "replace-", "me", async write =>
            {
                await write("start-");
                await write("before-replace-");
                await write("me-after");
                await write("-end");
            });

            Assert.Equal("start-before-with-this-after-end", output);
        }

        delegate ValueTask<FlushResult> Write(string text);

        private static async Task<string> UsingPipe(string localRoot, string localPath, string remoteRoot, string remotePath, Func<Write, Task> test)
        {
            var pipe = new Pipe();
            var replacer = new UrlRewriterMap(localRoot + localPath, remoteRoot + remotePath);
            var writer = new UrlRewritePipeWriter(pipe.Writer, new(localRoot, remoteRoot, [replacer]));

            Write write = async (string s) =>
            {
                await Task.Delay(1);
                return await writer.WriteAsync(Encoding.UTF8.GetBytes(s));
            };
            var readTask = Read(pipe.Reader);
            await test(write);
            await writer.CompleteAsync();
            var result = await readTask;
            await pipe.Reader.CompleteAsync();
            return result;
        }

        private static async Task<string> Read(PipeReader reader)
        {
            using var memory = new MemoryStream();
            using var stream = reader.AsStream();
            await StreamCopierFromYarpSourceCode.CopyAsync(stream, memory, -1, default);
            memory.Seek(0, SeekOrigin.Begin);
            using var strReader = new StreamReader(memory);
            return await strReader.ReadToEndAsync();
        }
    }
}
