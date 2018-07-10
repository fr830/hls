using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SwordsDance.Hls
{
    /// <summary>Reads HLS playlist data.</summary>
    public class PlaylistTextReader : IDisposable
    {
        private readonly TextReader _reader;
        private readonly UTF8Encoding _utf8;

        /// <summary>
        /// Initializes a new <see cref="PlaylistTextReader"/> instance with the specified <see cref="TextReader"/>.
        /// </summary>
        /// <param name="textReader">The <see cref="TextReader"/> containing HLS playlist data to be read.</param>
        /// <exception cref="ArgumentNullException"><paramref name="textReader"/> is <c>null</c>.</exception>
        public PlaylistTextReader(TextReader textReader)
        {
            _reader = textReader ?? throw new ArgumentNullException(nameof(textReader));
        }

        /// <summary>
        /// Initializes a new <see cref="PlaylistTextReader"/> instance with the specified stream.
        /// </summary>
        /// <param name="stream">The stream containing HLS playlist data to be read.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <c>null</c>.</exception>
        public PlaylistTextReader(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            _utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            _reader = new StreamReader(stream, _utf8);
            _shouldDisposeReader = true;
        }

        /// <summary>
        /// Initializes a new <see cref="PlaylistTextReader"/> with the specified string data.
        /// </summary>
        /// <param name="data">The string containing HLS playlist data to be read.</param>
        /// <exception cref="ArgumentNullException"><paramref name="data"/> is <c>null</c>.</exception>
        public PlaylistTextReader(string data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            _reader = new StringReader(data);
            _shouldDisposeReader = true;
        }

        /// <summary>Gets the current line number.</summary>
        /// <remarks>
        /// The returned value reflects the 1-based index of the line containing the character immediately following
        /// the most recently read token.
        /// </remarks>
        public int Line => throw new NotImplementedException();

        /// <summary>Gets the current character position.</summary>
        /// <remarks>
        /// The returned value reflects the 1-based index of character immediately following the most recently
        /// read token.
        /// </remarks>
        public int Column => throw new NotImplementedException();

        /// <summary>Gets the type of the most recently read token.</summary>
        public PlaylistTokenType TokenType { get; private set; }

        /// <summary>Gets the value of the most recently read token.</summary>
        public string TokenValue { get; private set; }

        /// <summary>Gets a boolean value indicating whether a UTF-8 byte order mark has been detected.</summary>
        public bool ByteOrderMarkDetected => _utf8 != null && ((StreamReader)_reader).CurrentEncoding != _utf8;

        /// <summary>Reads the next token in the playlist.</summary>
        /// <returns>
        /// <c>true</c> if a token was read and there are more tokens to read; otherwise, <c>false</c>
        /// </returns>
        public bool Read()
        {
            throw new NotImplementedException();
        }

        /// <summary>Reads the next token in the playlist and returns it.</summary>
        /// <returns>The read token, or <c>null</c> if there are no more tokens to read.</returns>
        public PlaylistToken? ReadToken()
        {
            throw new NotImplementedException();
        }

        /// <summary>Reads all remaining tokens in the playlist and returns them.</summary>
        /// <returns>An <see cref="IList{T}"/> containing the read tokens in the order they were read.</returns>
        public IList<PlaylistToken> ReadAllTokens()
        {
            throw new NotImplementedException();
        }

        #region IDisposable

        private readonly bool _shouldDisposeReader;
        private bool _disposed;

        /// <summary>Releases all resources used by the reader.</summary>
        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_shouldDisposeReader)
                    {
                        _reader.Dispose();
                    }
                }

                _disposed = true;
            }
        }

        #endregion IDisposable
    }
}
