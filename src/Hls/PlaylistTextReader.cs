using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SwordsDance.Hls
{
    /// <summary>Reads HLS playlist data.</summary>
    public partial class PlaylistTextReader : IDisposable
    {
        private const int DefaultBufferSize = 4096 / sizeof(char);

        private readonly TextReader _reader;
        private readonly UTF8Encoding _utf8;

        private char[] _buffer;
        private int _bufferedLength;
        private int _cursor;

        private int _lineNumber;
        private int _lineAnchor;

        private int _tokenAnchor;
        private int _tokenLine;
        private int _tokenColumn;

        private ReaderState _state;

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
        /// Initializes a new <see cref="PlaylistTextReader"/> instance with the specified <see cref="TextReader"/>.
        /// </summary>
        /// <param name="textReader">The <see cref="TextReader"/> containing HLS playlist data to be read.</param>
        /// <exception cref="ArgumentNullException"><paramref name="textReader"/> is <c>null</c>.</exception>
        public PlaylistTextReader(TextReader textReader)
        {
            _reader = textReader ?? throw new ArgumentNullException(nameof(textReader));
        }

        private enum ReaderState
        {
            UriOrCommentMarker,
            CommentOrTagName,
            TagNameValueSeparator, // on colon
            TagValueOrAttributeName,
            AttributeNameValueSeparator, // on equals sign
            AttributeValueOrQuotedAttributeValueMarker,
            QuotedAttributeValue,
            QuotedAttributeValueTerminator, // on double quote
            UnexpectedPostQuotedAttributeValueTerminatorData,
            AttributeSeparator, // on comma
            AttributeName, // any
            NewlineTerminatedValueTerminator, // on newline

            Error,
            EndOfFile,
        }

        /// <summary>Gets a boolean value indicating whether a UTF-8 byte order mark has been detected.</summary>
        /// <remarks>
        /// A UTF-8 byte order mark can only be detected if the reader was instantiated using the
        /// <see cref="PlaylistTextReader(Stream)"/> constructor overfload and will always return
        /// <c>false</c> otherwise.
        /// </remarks>
        public bool ByteOrderMarkDetected => _utf8 != null && ((StreamReader)_reader).CurrentEncoding != _utf8;

        /// <summary>Gets the current character position.</summary>
        /// <remarks>
        /// The returned value reflects the 1-based index of the character immediately following the most recently
        /// read token.
        /// </remarks>
        public int Column => _cursor - _lineAnchor + 1;

        /// <summary>Gets the current line number.</summary>
        /// <remarks>
        /// The returned value reflects the 1-based index of the line containing the character immediately following
        /// the most recently read token.
        /// </remarks>
        public int Line => _lineNumber + 1;

        /// <summary>Gets the character position of the most recently read token.</summary>
        /// <remarks>
        /// The returned value reflects the 1-based index of the first character of the value of the token.
        /// </remarks>
        public int TokenColumn { get; private set; }

        /// <summary>Gets the line number of the most recently read token.</summary>
        /// <remarks>
        /// The returned value reflects the 1-based index of the line containing the first character of the value of
        /// the token.
        /// </remarks>
        public int TokenLine { get; private set; }

        /// <summary>Gets the type of the most recently read token.</summary>
        public PlaylistTokenType TokenType { get; private set; }

        /// <summary>Gets the value of the most recently read token.</summary>
        public string TokenValue { get; private set; }

        /// <summary>Reads the next token in the playlist.</summary>
        /// <returns>
        /// <c>true</c> if a token was read; otherwise, <c>false</c>.
        /// </returns>
        public bool Read()
        {
            InitializeBuffer();

            while (true)
            {
                switch (_state)
                {
                    case ReaderState.UriOrCommentMarker:
                        ParseUriOrCommentMarker();
                        return true;
                    case ReaderState.CommentOrTagName:
                        ParseCommentOrTagName();
                        return true;
                    case ReaderState.TagNameValueSeparator:
                        ParseTagNameValueSeparator();
                        return true;
                    case ReaderState.TagValueOrAttributeName:
                        ParseTagValueOrAttributeName();
                        return true;
                    case ReaderState.AttributeNameValueSeparator:
                        ParseAttributeNameValueSeparator();
                        return true;
                    case ReaderState.AttributeValueOrQuotedAttributeValueMarker:
                        ParseAttributeValueOrQuotedAttributeValueMarker();
                        return true;
                    case ReaderState.QuotedAttributeValue:
                        ParseQuotedAttributeValue();
                        return true;
                    case ReaderState.QuotedAttributeValueTerminator:
                        ParseQuotedAttributeValueTerminator();
                        return true;
                    case ReaderState.UnexpectedPostQuotedAttributeValueTerminatorData:
                        ParseUnexpectedPostQuotedAttributeValueTerminatorData();
                        return true;
                    case ReaderState.AttributeSeparator:
                        ParseAttributeSeparator();
                        return true;
                    case ReaderState.AttributeName:
                        ParseAttributeName();
                        return true;
                    case ReaderState.NewlineTerminatedValueTerminator:
                        ParseNewlineTerminatedValueTerminator();
                        return true;
                    case ReaderState.Error:
                    case ReaderState.EndOfFile:
                        return false;
                    default:
                        throw new InvalidOperationException("Unreachable code reached.");
                }
            }
        }

        /// <summary>Reads all remaining tokens in the playlist and returns them.</summary>
        /// <returns>A <see cref="IList{T}"/> containing the read tokens in the order they were read.</returns>
        public IList<PlaylistToken> ReadAllTokens()
        {
            var tokens = new List<PlaylistToken>();
            while (Read())
            {
                tokens.Add(new PlaylistToken(TokenType, TokenValue, TokenLine, TokenColumn));
            }

            return tokens;
        }

        /// <summary>Reads the next token in the playlist and returns it.</summary>
        /// <returns>The read token, or <c>null</c> if there are no more tokens to read.</returns>
        public PlaylistToken ReadToken() => Read()
            ? new PlaylistToken(TokenType, TokenValue, TokenLine, TokenColumn)
            : null;

        private void InitializeBuffer()
        {
            if (_buffer == null)
            {
                _buffer = new char[DefaultBufferSize];
                _buffer[0] = '\0';
            }
        }

        private void ShiftBuffer()
        {
            int bufferLength = _buffer.Length;
            if (bufferLength - _cursor <= bufferLength / 16)
            {
                int copyLength = _bufferedLength - _cursor;
                if (copyLength > 0)
                {
                    Array.Copy(_buffer, _cursor, _buffer, 0, copyLength);
                }

                _lineAnchor -= _cursor;
                _cursor = 0;
                _bufferedLength = copyLength;
                _buffer[_bufferedLength] = '\0';
            }
        }

        private bool BufferData(int lookahead)
        {
            int requiredLength = _cursor + lookahead + 1;
            if (requiredLength < 0) requiredLength = 0x7FFFFFFF; // prevents integer overflow

            // resize the buffer to fit the required length
            int bufferLength = _buffer.Length;
            if (requiredLength >= bufferLength)
            {
                bufferLength = Math.Max(
                    bufferLength < 0x40000000 ? bufferLength * 2 : 0x7FFFFFFF, // prevents integer overflow
                    requiredLength);

                Array.Resize(ref _buffer, bufferLength);
            }

            // read characters until the required length has been reached or there are no more characters to read
            int previousBufferedLength;
            do
            {
                previousBufferedLength = _bufferedLength;

                _bufferedLength += _reader.Read(_buffer, _bufferedLength, bufferLength - _bufferedLength - 1);
            }
            while (_bufferedLength < requiredLength && _bufferedLength != previousBufferedLength);

            _buffer[_bufferedLength] = '\0';

            return _bufferedLength >= requiredLength;
        }

        private bool HasLookahead(int lookahead)
        {
            if (_cursor + lookahead >= _bufferedLength)
            {
                return BufferData(lookahead);
            }

            return true;
        }

        private bool IsEndOfBufferedData()
        {
            // must only be called after reading a null character
            Debug.Assert(_cursor <= _bufferedLength);
            Debug.Assert(_buffer[_cursor] == '\0');

            return _cursor == _bufferedLength;
        }

        private bool IsEndOfFile()
        {
            // must only be called after reading an end of buffered data-marking null character
            Debug.Assert(_cursor == _bufferedLength);
            Debug.Assert(_buffer[_cursor] == '\0');

            return !BufferData(lookahead: 0);
        }

        private bool IsCrLf()
        {
            // must only be called after reading a carriage return character
            Debug.Assert(_buffer[_cursor] == '\r');

            return HasLookahead(1) && _buffer[_cursor + 1] == '\n';
        }

        private void InitializeToken()
        {
            _tokenAnchor = _cursor;
            _tokenLine = Line;
            _tokenColumn = Column;
        }

        private void SetToken(PlaylistTokenType tokenType)
        {
            TokenType = tokenType;
            TokenValue = new string(_buffer, _tokenAnchor, _cursor - _tokenAnchor);
            TokenLine = _tokenLine;
            TokenColumn = _tokenColumn;
        }

        private void ParseUriOrCommentMarker()
        {
            Debug.Assert(_state == ReaderState.UriOrCommentMarker);

            ShiftBuffer();
            InitializeToken();

            if (HasLookahead(0) && _buffer[_cursor] == '#') // parse comment marker
            {
                _cursor++;
                SetToken(PlaylistTokenType.CommentMarker);
                _state = ReaderState.CommentOrTagName;
            }
            else // parse URI
            {
                while (true)
                {
                    switch (_buffer[_cursor])
                    {
                        case '\0' when IsEndOfBufferedData():
                            if (IsEndOfFile())
                            {
                                SetToken(PlaylistTokenType.Uri);
                                _state = ReaderState.EndOfFile;
                                return;
                            }
                            else continue;
                        case '\r' when IsCrLf():
                        case '\n':
                            SetToken(PlaylistTokenType.Uri);
                            _state = ReaderState.NewlineTerminatedValueTerminator;
                            return;
                        default:
                            _cursor++;
                            continue;
                    }
                }
            }
        }

        private void ParseCommentOrTagName()
        {
            Debug.Assert(_state == ReaderState.CommentOrTagName);

            ShiftBuffer();
            InitializeToken();

            if (HasLookahead(2)
                && _buffer[_cursor] == 'E'
                && _buffer[_cursor + 1] == 'X'
                && _buffer[_cursor + 2] == 'T') // parse tag name
            {
                _cursor += 3;

                while (true)
                {
                    switch (_buffer[_cursor])
                    {
                        case '\0' when IsEndOfBufferedData():
                            if (IsEndOfFile())
                            {
                                SetToken(PlaylistTokenType.TagName);
                                _state = ReaderState.EndOfFile;
                                return;
                            }
                            else continue;
                        case '\r' when IsCrLf():
                        case '\n':
                            SetToken(PlaylistTokenType.TagName);
                            _state = ReaderState.NewlineTerminatedValueTerminator;
                            return;
                        case ':':
                            SetToken(PlaylistTokenType.TagName);
                            _state = ReaderState.TagNameValueSeparator;
                            return;
                        default:
                            _cursor++;
                            continue;
                    }
                }
            }
            else // parse comment
            {
                while (true)
                {
                    switch (_buffer[_cursor])
                    {
                        case '\0' when IsEndOfBufferedData():
                            if (IsEndOfFile())
                            {
                                SetToken(PlaylistTokenType.Comment);
                                _state = ReaderState.EndOfFile;
                                return;
                            }
                            else continue;
                        case '\r' when IsCrLf():
                        case '\n':
                            SetToken(PlaylistTokenType.Comment);
                            _state = ReaderState.NewlineTerminatedValueTerminator;
                            return;
                        default:
                            _cursor++;
                            continue;
                    }
                }
            }
        }

        private void ParseTagNameValueSeparator()
        {
            Debug.Assert(_state == ReaderState.TagNameValueSeparator);
            Debug.Assert(_buffer[_cursor] == ':');

            InitializeToken();
            _cursor++;
            SetToken(PlaylistTokenType.TagNameValueSeparator);
            _state = ReaderState.TagValueOrAttributeName;
        }

        private void ParseTagValueOrAttributeName()
        {
            Debug.Assert(_state == ReaderState.TagValueOrAttributeName);

            ShiftBuffer();
            InitializeToken();

            bool isDefinitelyTagName = false;
            while (true)
            {
                switch (_buffer[_cursor])
                {
                    case '\0' when IsEndOfBufferedData():
                        if (IsEndOfFile())
                        {
                            SetToken(PlaylistTokenType.TagValue);
                            _state = ReaderState.EndOfFile;
                            return;
                        }
                        else continue;
                    case '\r' when IsCrLf():
                    case '\n':
                        SetToken(PlaylistTokenType.TagValue);
                        _state = ReaderState.NewlineTerminatedValueTerminator;
                        return;
                    case '=' when !isDefinitelyTagName:
                        if (isValidAttributeName())
                        {
                            SetToken(PlaylistTokenType.AttributeName);
                            _state = ReaderState.AttributeNameValueSeparator;
                            return;
                        }
                        else
                        {
                            isDefinitelyTagName = true;
                            goto default;
                        }
                    default:
                        _cursor++;
                        continue;
                }
            }

            bool isValidAttributeName()
            {
                for (int i = _tokenAnchor; i < _cursor; i++)
                {
                    char ch = _buffer[i];
                    if ((ch < 'A' || ch > 'Z') && (ch < '0' || ch > '9') && ch != '-') return false;
                }

                return true;
            }
        }

        private void ParseAttributeNameValueSeparator()
        {
            Debug.Assert(_state == ReaderState.AttributeNameValueSeparator);
            Debug.Assert(_buffer[_cursor] == '=');

            InitializeToken();
            _cursor++;
            SetToken(PlaylistTokenType.AttributeNameValueSeparator);
            _state = ReaderState.AttributeValueOrQuotedAttributeValueMarker;
        }

        private void ParseAttributeValueOrQuotedAttributeValueMarker()
        {
            Debug.Assert(_state == ReaderState.AttributeValueOrQuotedAttributeValueMarker);

            ShiftBuffer();
            InitializeToken();

            if (HasLookahead(0) && _buffer[_cursor] == '"') // parse quoted attribute value marker
            {
                _cursor++;
                SetToken(PlaylistTokenType.QuotedAttributeValueMarker);
                _state = ReaderState.QuotedAttributeValue;
            }
            else // parse attribute value
            {
                while (true)
                {
                    switch (_buffer[_cursor])
                    {
                        case '\0' when IsEndOfBufferedData():
                            if (IsEndOfFile())
                            {
                                SetToken(PlaylistTokenType.AttributeValue);
                                _state = ReaderState.EndOfFile;
                                return;
                            }
                            else continue;
                        case '\r' when IsCrLf():
                        case '\n':
                            SetToken(PlaylistTokenType.AttributeValue);
                            _state = ReaderState.NewlineTerminatedValueTerminator;
                            return;
                        case ',':
                            SetToken(PlaylistTokenType.AttributeValue);
                            _state = ReaderState.AttributeSeparator;
                            return;
                        default:
                            _cursor++;
                            continue;
                    }
                }
            }
        }

        private void ParseQuotedAttributeValue()
        {
            Debug.Assert(_state == ReaderState.QuotedAttributeValue);

            ShiftBuffer();
            InitializeToken();

            while (true)
            {
                switch (_buffer[_cursor])
                {
                    case '\0' when IsEndOfBufferedData():
                        if (IsEndOfFile())
                        {
                            SetToken(PlaylistTokenType.AttributeValue);
                            _state = ReaderState.EndOfFile;
                            return;
                        }
                        else continue;
                    case '\r' when IsCrLf():
                    case '\n':
                        SetToken(PlaylistTokenType.AttributeValue);
                        _state = ReaderState.NewlineTerminatedValueTerminator;
                        return;
                    case '"':
                        SetToken(PlaylistTokenType.AttributeValue);
                        _state = ReaderState.QuotedAttributeValueTerminator;
                        return;
                    default:
                        _cursor++;
                        continue;
                }
            }
        }

        private void ParseQuotedAttributeValueTerminator()
        {
            Debug.Assert(_state == ReaderState.QuotedAttributeValueTerminator);
            Debug.Assert(_buffer[_cursor] == '"');

            ShiftBuffer();
            InitializeToken();
            _cursor++;

            while (true)
            {
                switch (_buffer[_cursor])
                {
                    case '\0' when IsEndOfBufferedData():
                        if (IsEndOfFile())
                        {
                            SetToken(PlaylistTokenType.QuotedAttributeValueTerminator);
                            _state = ReaderState.EndOfFile;
                            return;
                        }
                        else continue;
                    case '\r' when IsCrLf():
                    case '\n':
                        SetToken(PlaylistTokenType.QuotedAttributeValueTerminator);
                        _state = ReaderState.NewlineTerminatedValueTerminator;
                        return;
                    case ',':
                        SetToken(PlaylistTokenType.QuotedAttributeValueTerminator);
                        _state = ReaderState.AttributeSeparator;
                        return;
                    default:
                        SetToken(PlaylistTokenType.QuotedAttributeValueTerminator);
                        _state = ReaderState.UnexpectedPostQuotedAttributeValueTerminatorData;
                        return;
                }
            }
        }

        private void ParseUnexpectedPostQuotedAttributeValueTerminatorData()
        {
            ShiftBuffer();
            InitializeToken();

            while (true)
            {
                switch (_buffer[_cursor])
                {
                    case '\0' when IsEndOfBufferedData():
                        if (IsEndOfFile())
                        {
                            SetToken(PlaylistTokenType.UnexpectedPostQuotedAttributeValueTerminatorData);
                            _state = ReaderState.EndOfFile;
                            return;
                        }
                        else continue;
                    case '\r' when IsCrLf():
                    case '\n':
                        SetToken(PlaylistTokenType.UnexpectedPostQuotedAttributeValueTerminatorData);
                        _state = ReaderState.NewlineTerminatedValueTerminator;
                        return;
                    case ',':
                        SetToken(PlaylistTokenType.UnexpectedPostQuotedAttributeValueTerminatorData);
                        _state = ReaderState.AttributeSeparator;
                        return;
                    default:
                        _cursor++;
                        continue;
                }
            }
        }

        private void ParseAttributeSeparator()
        {
            Debug.Assert(_state == ReaderState.AttributeSeparator);
            Debug.Assert(_buffer[_cursor] == ',');

            InitializeToken();
            _cursor++;
            SetToken(PlaylistTokenType.AttributeSeparator);
            _state = ReaderState.AttributeName;
        }

        private void ParseAttributeName()
        {
            Debug.Assert(_state == ReaderState.AttributeName);

            ShiftBuffer();
            InitializeToken();

            while (true)
            {
                switch (_buffer[_cursor])
                {
                    case '\0' when IsEndOfBufferedData():
                        if (IsEndOfFile())
                        {
                            SetToken(PlaylistTokenType.AttributeName);
                            _state = ReaderState.EndOfFile;
                            return;
                        }
                        else continue;
                    case '\r' when IsCrLf():
                    case '\n':
                        SetToken(PlaylistTokenType.AttributeName);
                        _state = ReaderState.NewlineTerminatedValueTerminator;
                        return;
                    case '=':
                        SetToken(PlaylistTokenType.AttributeName);
                        _state = ReaderState.AttributeNameValueSeparator;
                        return;
                    default:
                        _cursor++;
                        continue;
                }
            }
        }

        private void ParseNewlineTerminatedValueTerminator()
        {
            Debug.Assert(_state == ReaderState.NewlineTerminatedValueTerminator);
            Debug.Assert(_buffer[_cursor] == '\n' || _buffer[_cursor] == '\r' && _buffer[_cursor + 1] == '\n');

            InitializeToken();
            _cursor += _buffer[_cursor] == '\r' ? 2 : 1;
            _lineNumber++;
            _lineAnchor = _cursor;
            SetToken(PlaylistTokenType.NewlineTerminatedValueTerminator);
            _state = ReaderState.UriOrCommentMarker;
        }

        #region IDisposable

        private readonly bool _shouldDisposeReader;
        private bool _disposed;

        /// <summary>Releases all resources used by the reader.</summary>
        public void Dispose() => Dispose(true);

        /// <summary>
        /// Releases unmanaged and optionally managed resources used by the reader.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only
        /// unmanaged resources.
        /// </param>
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
