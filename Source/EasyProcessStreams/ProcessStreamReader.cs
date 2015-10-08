using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Narkhedegs
{
    public sealed class ProcessStreamReader : TextReader
    {
        /// <summary>
        /// The underlying <see cref="Stream"/> from the <see cref="Process"/>
        /// </summary>
        private readonly Stream _processStream;
        private readonly Pipe _pipe;
        private readonly StreamReader _reader;
        private volatile bool _discardContents;

        public ProcessStreamReader(StreamReader processStreamReader)
        {
            _processStream = processStreamReader.BaseStream;
            _pipe = new Pipe();
            _reader = new StreamReader(_pipe.OutputStream);
            Task = Task.Run(() => BufferLoop());
        }

        public Task Task { get; }

        private async Task BufferLoop()
        {
            try
            {
                var buffer = new byte[Pipe.ByteBufferSize];
                int bytesRead;
                while (
                    !_discardContents
                    && (bytesRead = await _processStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0
                )
                {
                    await _pipe.InputStream.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
                }
            }
            finally
            {
                _processStream.Close();
                _pipe.InputStream.Close();
            }
        }

        public Stream BaseStream => _reader.BaseStream;

        /// <summary>
        /// Enumerates each remaining line of output. The enumerable cannot be re-used
        /// </summary>
        public IEnumerable<string> GetLines()
        {
            return new LinesEnumerable(this);
        }

        private class LinesEnumerable : IEnumerable<string>
        {
            private readonly TextReader _reader;
            private int _consumed;

            public LinesEnumerable(TextReader reader)
            {
                _reader = reader;
            }

            IEnumerator<string> IEnumerable<string>.GetEnumerator()
            {
                if (Interlocked.Exchange(ref _consumed, 1) != 0)
                {
                    throw new InvalidOperationException("The enumerable returned by GetLines() can only be enumerated once.");
                }

                return GetEnumeratorInternal();
            }

            private IEnumerator<string> GetEnumeratorInternal()
            {
                string line;
                while ((line = _reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return this.AsEnumerable().GetEnumerator();
            }
        }

        /// <summary>
        /// Discards all output from the underlying stream. This prevents the process from blocking because
        /// the output pipe's buffer is full without wasting any memory on buffering the output
        /// </summary>
        public void Discard()
        {
            _discardContents = true;
            _reader.Dispose();
        }

        /// <summary>
        /// By default, the underlying stream output is buffered to prevent the process from blocking
        /// because the output pipe's buffer is full. Calling this method disables this behavior. This is useful
        /// when it is desirable to have the process block while waiting for output to be read
        /// </summary>
        public void StopBuffering()
        {
            // this causes writes to the pipe to block, thus
            // preventing unbounded buffering (although some more content
            // may still be buffered)
            _pipe.SetFixedLength();
        }

        /// <summary>
        /// Pipes the output of the underlying stream to the given stream. This occurs asynchronously, so this
        /// method returns before all content has been written
        /// </summary>
        public Task PipeToAsync(Stream stream, bool leaveReaderOpen = false, bool leaveStreamOpen = false)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            return this.PipeAsync(
                () => BaseStream.CopyToAsync(stream), leaveReaderOpen,
                leaveStreamOpen ? default(Action) : () => stream.Dispose()
                );
        }

        /// <summary>
        /// Pipes the output of the reader to the given writer. This occurs asynchronously, so this
        /// method returns before all content has been written
        /// </summary>
        public Task PipeToAsync(TextWriter writer, bool leaveReaderOpen = false, bool leaveWriterOpen = false)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            return this.CopyToAsync(writer, leaveReaderOpen, leaveWriterOpen);
        }

        /// <summary>
        /// Asynchronously copies each line of output to the given collection
        /// </summary>
        public Task PipeToAsync(ICollection<string> lines, bool leaveReaderOpen = false)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            return this.PipeAsync(
                async () =>
                {
                    string line;
                    while ((line = await ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        lines.Add(line);
                    }
                }, leaveReaderOpen
            );
        }

        /// <summary>
        /// Asynchronously writes all output to the given file
        /// </summary>
        public Task PipeToAsync(FileInfo file, bool leaveReaderOpen = false)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            // used over FileInfo.OpenWrite to get read file share, which seems potentially useful and
            // not that harmful
            var stream = new FileStream(file.FullName, FileMode.Create, FileAccess.Write, FileShare.Read);
            return PipeToAsync(stream, leaveReaderOpen);
        }

        /// <summary>
        /// Asynchronously copies each charater to the given collection
        /// </summary>
        public Task PipeToAsync(ICollection<char> chars, bool leaveReaderOpen = false)
        {
            if (chars == null)
            {
                throw new ArgumentNullException(nameof(chars));
            }

            return this.PipeAsync(
                async () =>
                {
                    var buffer = new char[Pipe.CharBufferSize];
                    int bytesRead;
                    while ((bytesRead = await ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0)
                    {
                        for (var i = 0; i < bytesRead; ++i)
                        {
                            chars.Add(buffer[i]);
                        }
                    }
                }, leaveReaderOpen
            );
        }

        #region ---- TextReader implementation ----
        // all reader methods are overriden to call the same method on the underlying StreamReader.
        // This approach is preferable to extending StreamReader directly, since many of the async methods
        // on StreamReader are conservative and fall back to threaded asynchrony when inheritance is in play
        // (this is done to respect any overriden Read() call). This way, we get the full benefit of asynchrony.

        public override int Peek()
        {
            return _reader.Peek();
        }

        public override int Read()
        {
            return _reader.Read();
        }

        public override int Read(char[] buffer, int index, int count)
        {
            return _reader.Read(buffer, index, count);
        }

        public override Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            return _reader.ReadAsync(buffer, index, count);
        }

        public override int ReadBlock(char[] buffer, int index, int count)
        {
            return _reader.ReadBlock(buffer, index, count);
        }

        public override Task<int> ReadBlockAsync(char[] buffer, int index, int count)
        {
            return _reader.ReadBlockAsync(buffer, index, count);
        }

        public override string ReadLine()
        {
            return _reader.ReadLine();
        }

        public override Task<string> ReadLineAsync()
        {
            return _reader.ReadLineAsync();
        }

        public override string ReadToEnd()
        {
            return _reader.ReadToEnd();
        }

        public override Task<string> ReadToEndAsync()
        {
            return _reader.ReadToEndAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Discard();
            }
        }
        #endregion
    }
}