using System.IO.Pipelines;
using System.Text;
using PodiumdAdapter.Web.Infrastructure.UrlRewriter;

namespace PodiumdAdapter.Web.Test
{
    public class UrlRewritePipeWriterTest
    {
        [Fact]
        public async Task BasicTest()
        {
            var localString = "local";
            var remoteString = "remote";
            var (read, write) = CreatePipe(localString, remoteString);
            
            await write(remoteString);
            
            var output = await read();
            Assert.Equal(localString, output);
        }

        private static (Func<Task<string>> Read, Func<string,Task> Write) CreatePipe(string localString, string remoteString)
        {
            var pipe = new Pipe();
            var reader = pipe.Reader;
            var localBytes = Encoding.UTF8.GetBytes(localString);
            var remoteBytes = Encoding.UTF8.GetBytes(remoteString);
            var replacer = new Replacer(remoteBytes, localBytes, remoteString, localString);
            var writer = new UrlRewritePipeWriter(pipe.Writer, [replacer]);
            return (() => ReadToEnd(reader), (s) => WriteToEnd(writer, s));
        }

        private static async Task WriteToEnd(PipeWriter writer, string str)
        {
            await writer.WriteAsync(Encoding.UTF8.GetBytes(str));
            await writer.CompleteAsync();
        }

        private static async Task<string> ReadToEnd(PipeReader reader)
        {
            var data = await reader.ReadAsync();

            while (!data.IsCompleted && !data.IsCanceled)
            {
                reader.AdvanceTo(data.Buffer.Start, data.Buffer.End);
                data = await reader.ReadAsync();
            }

            var output = Encoding.UTF8.GetString(data.Buffer);

            await reader.CompleteAsync();
            return output;
        }
    }
}
