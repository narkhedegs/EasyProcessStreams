using System;
using System.IO;
using System.Threading.Tasks;

namespace Narkhedegs
{
    internal static class PipeHelpers
    {
        public static Task CopyToAsync(this TextReader reader, TextWriter writer, bool leaveReaderOpen,
            bool leaveWriterOpen)
        {
            return reader.PipeAsync(
                async () =>
                {
                    var buffer = new char[Pipe.CharBufferSize];
                    int charsRead;
                    while ((charsRead = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                    {
                        await writer.WriteAsync(buffer, 0, charsRead).ConfigureAwait(false);
                    }
                }, leaveReaderOpen, leaveWriterOpen ? default(Action) : () => writer.Dispose()
                );
        }

        public static async Task PipeAsync(this IDisposable @this, Func<Task> pipeTaskFactory, bool leaveOpen,
            Action extraDisposeAction = null)
        {
            try
            {
                await pipeTaskFactory().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!ex.IsExpectedPipeException())
                {
                    throw;
                }
            }
            finally
            {
                if (!leaveOpen)
                {
                    @this.Dispose();
                }
                extraDisposeAction?.Invoke();
            }
        }
    }
}