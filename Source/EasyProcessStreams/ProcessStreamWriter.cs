using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Narkhedegs
{
    /// <summary>
    /// Provides functionality similar to a <see cref="StreamWriter"/> but with additional methods to simplify
    /// working with a process's standard input
    /// </summary>
    public sealed class ProcessStreamWriter : TextWriter
    {
        private readonly StreamWriter _writer;

        public ProcessStreamWriter(StreamWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            _writer = writer;
            AutoFlush = true; // set the default
        }

        #region ---- Custom methods ----
        /// <summary>
        /// Provides access to the underlying <see cref="Stream"/>. Equivalent to <see cref="StreamWriter.BaseStream"/>
        /// </summary>
        public Stream BaseStream => _writer.BaseStream;

        /// <summary>
        /// Determines whether writes are automatically flushed to the underlying <see cref="Stream"/> after each write.
        /// Equivalent to <see cref="StreamWriter.AutoFlush"/>. Defaults to TRUE
        /// </summary>
        public bool AutoFlush
        {
            get { return _writer.AutoFlush; }
            set { _writer.AutoFlush = value; }
        }

        /// <summary>
        /// Asynchronously copies <paramref name="stream"/> to this stream
        /// </summary>
        public Task PipeFromAsync(Stream stream, bool leaveWriterOpen = false, bool leaveStreamOpen = false)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            return this.PipeAsync(
                async () =>
                {
                    // flush any content buffered in the writer, since we'll be using the raw stream
                    await _writer.FlushAsync().ConfigureAwait(false);
                    await stream.CopyToAsync(BaseStream).ConfigureAwait(false);
                }, leaveWriterOpen, leaveStreamOpen ? default(Action) : () => stream.Dispose());
        }

        /// <summary>
        /// Asynchronously writes each item in <paramref name="lines"/> to this writer as a separate line
        /// </summary>
        public Task PipeFromAsync(IEnumerable<string> lines, bool leaveWriterOpen = false)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            return this.PipeAsync(
                // wrap in Task.Run since GetEnumerator() or MoveNext() might block
                () => Task.Run(async () =>
                {
                    foreach (var line in lines)
                    {
                        await WriteLineAsync(line).ConfigureAwait(false);
                    }
                }), leaveWriterOpen
            );
        }

        /// <summary>
        /// Asynchronously writes all content from <paramref name="reader"/> to this writer
        /// </summary>
        public Task PipeFromAsync(TextReader reader, bool leaveWriterOpen = false, bool leaveReaderOpen = false)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            return reader.CopyToAsync(_writer, leaveReaderOpen, leaveWriterOpen);
        }

        /// <summary>
        /// Asynchronously writes all content from <paramref name="file"/> to this stream
        /// </summary>
        public Task PipeFromAsync(FileInfo file, bool leaveWriterOpen = false)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            var stream = file.OpenRead();
            return PipeFromAsync(stream, leaveWriterOpen);
        }

        /// <summary>
        /// Asynchronously writes all content from <paramref name="chars"/> to this writer
        /// </summary>
        public Task PipeFromAsync(IEnumerable<char> chars, bool leaveWriterOpen = false)
        {
            if (chars == null)
            {
                throw new ArgumentNullException(nameof(chars));
            }

            var @string = chars as string;
            return this.PipeAsync(
                @string != null
                    // special-case string since we can use the built-in WriteAsync
                    ? new Func<Task>(() => WriteAsync(@string))
                    // when enumerating, layer on a Task.Run since GetEnumerator() or MoveNext() might block
                    : () => Task.Run(async () =>
                    {
                        var buffer = new char[Pipe.CharBufferSize];
                        using (var enumerator = chars.GetEnumerator())
                        {
                            while (true)
                            {
                                var i = 0;
                                while (i < buffer.Length && enumerator.MoveNext())
                                {
                                    buffer[i++] = enumerator.Current;
                                }
                                if (i > 0)
                                {
                                    await WriteAsync(buffer, 0, count: i).ConfigureAwait(false);
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    }), leaveWriterOpen
                );
        }
        #endregion

        #region ---- TextWriter methods ----
        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _writer.Dispose();
            }
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override Encoding Encoding => _writer.Encoding;

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Flush()
        {
            _writer.Flush();
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override Task FlushAsync()
        {
            return _writer.FlushAsync();
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override IFormatProvider FormatProvider => _writer.FormatProvider;

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override string NewLine { get { return _writer.NewLine; } set { _writer.NewLine = value; } }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(bool value)
        {
            _writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(char value)
        {
            _writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(char[] buffer)
        {
            _writer.Write(buffer);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(char[] buffer, int index, int count)
        {
            _writer.Write(buffer, index, count);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(decimal value)
        {
            _writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(double value)
        {
            _writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(float value)
        {
            _writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(int value)
        {
            _writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(long value)
        {
            _writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(object value)
        {
            _writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(string format, object arg0)
        {
            _writer.Write(format, arg0);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(string format, object arg0, object arg1)
        {
            _writer.Write(format, arg0, arg1);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(string format, object arg0, object arg1, object arg2)
        {
            _writer.Write(format, arg0, arg1, arg2);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(string format, params object[] arg)
        {
            _writer.Write(format, arg);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(string value)
        {
            _writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(uint value)
        {
            _writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(ulong value)
        {
            _writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override Task WriteAsync(char value)
        {
            return _writer.WriteAsync(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override Task WriteAsync(char[] buffer, int index, int count)
        {
            return _writer.WriteAsync(buffer, index, count);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override Task WriteAsync(string value)
        {
            return _writer.WriteAsync(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine()
        {
            _writer.WriteLine();
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(bool value)
        {
            _writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(char value)
        {
            _writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(char[] buffer)
        {
            _writer.WriteLine(buffer);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(char[] buffer, int index, int count)
        {
            _writer.WriteLine(buffer, index, count);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(decimal value)
        {
            _writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(double value)
        {
            _writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(float value)
        {
            _writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(int value)
        {
            _writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(long value)
        {
            _writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(object value)
        {
            _writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(string format, object arg0)
        {
            _writer.WriteLine(format, arg0);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(string format, object arg0, object arg1)
        {
            _writer.WriteLine(format, arg0, arg1);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(string format, object arg0, object arg1, object arg2)
        {
            _writer.WriteLine(format, arg0, arg1, arg2);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(string format, params object[] arg)
        {
            _writer.WriteLine(format, arg);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(string value)
        {
            _writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(uint value)
        {
            _writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(ulong value)
        {
            _writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override Task WriteLineAsync()
        {
            return _writer.WriteLineAsync();
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override Task WriteLineAsync(char value)
        {
            return _writer.WriteLineAsync(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override Task WriteLineAsync(char[] buffer, int index, int count)
        {
            return _writer.WriteLineAsync(buffer, index, count);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override Task WriteLineAsync(string value)
        {
            return _writer.WriteLineAsync(value);
        }
        #endregion
    }
}