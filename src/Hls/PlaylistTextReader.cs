using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SwordsDance.Hls
{
    /// <summary>Reads HLS playlist data.</summary>
    public class PlaylistTextReader : IDisposable
    {
        private const int DefaultBufferSize = 4096 / sizeof(char);

        private readonly TextReader _reader;
        private readonly UTF8Encoding _utf8;

        private char[] _buffer;
        private int _bufferedLength;
        private int _cursor;
        private int _lineNumber;
        private int _lineAnchor;
        private int _valueAnchor;

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
            LineValue, // on any
            TagValueOrAttributeName, // on colon
            AttributeValue, // on equals sign
            PostQuotedAttributeValue, // on double quote
            AttributeName, // on comma
            PostLineValue, // on newline

            Error,
            EndOfStream,
        }

        /// <summary>Gets a boolean value indicating whether a UTF-8 byte order mark has been detected.</summary>
        public bool ByteOrderMarkDetected => _utf8 != null && ((StreamReader)_reader).CurrentEncoding != _utf8;

        /// <summary>Gets the current character position.</summary>
        /// <remarks>
        /// The returned value reflects the 1-based index of character immediately following the most recently
        /// read token.
        /// </remarks>
        public int Column => _cursor - _lineAnchor + 1;

        /// <summary>Gets the current line number.</summary>
        /// <remarks>
        /// The returned value reflects the 1-based index of the line containing the character immediately following
        /// the most recently read token.
        /// </remarks>
        public int Line => _lineNumber + 1;

        /// <summary>Gets the type of the most recently read token.</summary>
        public PlaylistTokenType TokenType { get; private set; }

        /// <summary>Gets the value of the most recently read token.</summary>
        public string TokenValue { get; private set; }

        /// <summary>Reads the next token in the playlist.</summary>
        /// <returns>
        /// <c>true</c> if a token was read and there are more tokens to read; otherwise, <c>false</c>
        /// </returns>
        public bool Read()
        {
            InitializeBuffer();

            while (true)
            {
                switch (_state)
                {
                    case ReaderState.LineValue:
                        ParseLineValue();
                        return true;
                    case ReaderState.TagValueOrAttributeName:
                        ParseTagValueOrAttributeName();
                        return true;
                    case ReaderState.AttributeValue:
                        ParseAttributeValue();
                        return true;
                    case ReaderState.PostQuotedAttributeValue:
                        if (HandlePostQuotedAttributeValue()) continue;
                        else return false;
                    case ReaderState.AttributeName:
                        ParseAttributeName();
                        return true;
                    case ReaderState.PostLineValue:
                        if (HandlePostLineValue()) continue;
                        else return false;
                    case ReaderState.Error:
                    case ReaderState.EndOfStream:
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
                tokens.Add(new PlaylistToken(TokenType, TokenValue));
            }

            return tokens;
        }

        /// <summary>Reads the next token in the playlist and returns it.</summary>
        /// <returns>The read token, or <c>null</c> if there are no more tokens to read.</returns>
        public PlaylistToken? ReadToken() => Read() ? new PlaylistToken(TokenType, TokenValue) : (PlaylistToken?)null;

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

        private bool IsEndOfStream()
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

        private void SetToken(PlaylistTokenType tokenType)
        {
            TokenType = tokenType;
            TokenValue = new string(_buffer, _valueAnchor, _cursor - _valueAnchor);
        }

        private void ParseLineValue()
        {
            Debug.Assert(_state == ReaderState.LineValue);

            ShiftBuffer();

            while (true)
            {
                switch (_buffer[_cursor])
                {
                    case '\0' when IsEndOfBufferedData():
                        if (IsEndOfStream())
                        {
                            // will be handled by the URI parsing procedure
                            goto default;
                        }
                        else continue;
                    case '#':
                        if (HasLookahead(3)
                            && _buffer[_cursor + 1] == 'E'
                            && _buffer[_cursor + 2] == 'X'
                            && _buffer[_cursor + 3] == 'T')
                        {
                            parseTagName();
                            return;
                        }
                        else
                        {
                            parseComment();
                            return;
                        }
                    default:
                        parseUriOrBlank();
                        return;
                }
            }

            void parseTagName()
            {
                Debug.Assert(_buffer[_cursor] == '#');
                Debug.Assert(_buffer[_cursor + 1] == 'E');
                Debug.Assert(_buffer[_cursor + 2] == 'X');
                Debug.Assert(_buffer[_cursor + 3] == 'T');

                _valueAnchor = _cursor + 1;
                _cursor += 4;

                while (true)
                {
                    switch (_buffer[_cursor])
                    {
                        case '\0' when IsEndOfBufferedData():
                            if (IsEndOfStream())
                            {
                                SetToken(PlaylistTokenType.TagName);
                                _state = ReaderState.EndOfStream;
                                return;
                            }
                            else continue;
                        case '\r' when IsCrLf():
                        case '\n':
                            SetToken(PlaylistTokenType.TagName);
                            _state = ReaderState.PostLineValue;
                            return;
                        case ':':
                            SetToken(PlaylistTokenType.TagName);
                            _state = ReaderState.TagValueOrAttributeName;
                            return;
                        default:
                            _cursor++;
                            continue;
                    }
                }
            }

            void parseComment()
            {
                Debug.Assert(_buffer[_cursor] == '#');

                _valueAnchor = ++_cursor;

                while (true)
                {
                    switch (_buffer[_cursor])
                    {
                        case '\0' when IsEndOfBufferedData():
                            if (IsEndOfStream())
                            {
                                SetToken(PlaylistTokenType.Comment);
                                _state = ReaderState.EndOfStream;
                                return;
                            }
                            else continue;
                        case '\r' when IsCrLf():
                        case '\n':
                            SetToken(PlaylistTokenType.Comment);
                            _state = ReaderState.PostLineValue;
                            return;
                        default:
                            _cursor++;
                            continue;
                    }
                }
            }

            void parseUriOrBlank()
            {
                _valueAnchor = _cursor;

                while (true)
                {
                    switch (_buffer[_cursor])
                    {
                        case '\0' when IsEndOfBufferedData():
                            if (IsEndOfStream())
                            {
                                SetToken(determineTokenType());
                                _state = ReaderState.EndOfStream;
                                return;
                            }
                            else continue;
                        case '\r' when IsCrLf():
                        case '\n':
                            SetToken(determineTokenType());
                            _state = ReaderState.PostLineValue;
                            return;
                        default:
                            _cursor++;
                            continue;
                    }
                }

                PlaylistTokenType determineTokenType()
                {
                    for (int i = _valueAnchor; i < _cursor; i++)
                    {
                        char ch = _buffer[i];
                        if (ch != ' ' && (ch > 0xD || ch < 0x9)) return PlaylistTokenType.Uri;
                    }

                    return PlaylistTokenType.Blank;
                }
            }
        }

        private void ParseTagValueOrAttributeName()
        {
            Debug.Assert(_state == ReaderState.TagValueOrAttributeName);
            Debug.Assert(_buffer[_cursor] == ':');

            ShiftBuffer();

            _valueAnchor = ++_cursor;

            bool isDefinitelyTagName = false;
            while (true)
            {
                switch (_buffer[_cursor])
                {
                    case '\0' when IsEndOfBufferedData():
                        if (IsEndOfStream())
                        {
                            SetToken(PlaylistTokenType.TagValue);
                            _state = ReaderState.EndOfStream;
                            return;
                        }
                        else continue;
                    case '\r' when IsCrLf():
                    case '\n':
                        SetToken(PlaylistTokenType.TagValue);
                        _state = ReaderState.PostLineValue;
                        return;
                    case '=' when !isDefinitelyTagName:
                        if (isValidAttributeName())
                        {
                            SetToken(PlaylistTokenType.AttributeName);
                            _state = ReaderState.AttributeValue;
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
                for (int i = _valueAnchor; i < _cursor; i++)
                {
                    char ch = _buffer[i];
                    if ((ch < 'A' || ch > 'Z') && (ch < '0' || ch > '9') && ch != '-') return false;
                }

                return true;
            }
        }

        private void ParseAttributeValue()
        {
            Debug.Assert(_state == ReaderState.AttributeValue);
            Debug.Assert(_buffer[_cursor] == '=');

            ShiftBuffer();

            if (HasLookahead(1) && _buffer[_cursor + 1] == '"') // parse quoted value
            {
                _cursor += 2;
                _valueAnchor = _cursor;

                while (true)
                {
                    switch (_buffer[_cursor])
                    {
                        case '\0' when IsEndOfBufferedData():
                            if (IsEndOfStream())
                            {
                                _state = ReaderState.Error;
                                throw new FormatException(); // unexpected end of stream
                            }
                            else continue;
                        case '\r' when IsCrLf():
                        case '\n':
                            _state = ReaderState.Error;
                            throw new FormatException(); // unexpected newline
                        case '"':
                            SetToken(PlaylistTokenType.QuotedAttributeValue);
                            _state = ReaderState.PostQuotedAttributeValue;
                            return;
                        default:
                            _cursor++;
                            continue;
                    }
                }
            }
            else // parse unquoted value
            {
                _valueAnchor = ++_cursor;

                while (true)
                {
                    switch (_buffer[_cursor])
                    {
                        case '\0' when IsEndOfBufferedData():
                            if (IsEndOfStream())
                            {
                                SetToken(PlaylistTokenType.UnquotedAttributeValue);
                                _state = ReaderState.EndOfStream;
                                return;
                            }
                            else continue;
                        case '\r' when IsCrLf():
                        case '\n':
                            SetToken(PlaylistTokenType.UnquotedAttributeValue);
                            _state = ReaderState.PostLineValue;
                            return;
                        case ',':
                            SetToken(PlaylistTokenType.UnquotedAttributeValue);
                            _state = ReaderState.AttributeName;
                            return;
                        default:
                            _cursor++;
                            continue;
                    }
                }
            }
        }

        private bool HandlePostQuotedAttributeValue()
        {
            Debug.Assert(_state == ReaderState.PostQuotedAttributeValue);
            Debug.Assert(_buffer[_cursor] == '"');

            ShiftBuffer();

            _cursor++;

            while (true)
            {
                switch (_buffer[_cursor])
                {
                    case '\0' when IsEndOfBufferedData():
                        if (IsEndOfStream())
                        {
                            _state = ReaderState.EndOfStream;
                            return false;
                        }
                        else continue;
                    case '\r' when IsCrLf():
                    case '\n':
                        _state = ReaderState.PostLineValue;
                        return true;
                    case ',':
                        _state = ReaderState.AttributeName;
                        return true;
                    default:
                        _state = ReaderState.Error;
                        throw new FormatException(); // unexpected character
                }
            }
        }

        private void ParseAttributeName()
        {
            Debug.Assert(_state == ReaderState.AttributeName);
            Debug.Assert(_buffer[_cursor] == ',');

            ShiftBuffer();

            _valueAnchor = ++_cursor;

            while (true)
            {
                switch (_buffer[_cursor])
                {
                    case '\0' when IsEndOfBufferedData():
                        if (IsEndOfStream())
                        {
                            _state = ReaderState.Error;
                            throw new FormatException(); // unexpected end of stream
                        }
                        else continue;
                    case '\r' when IsCrLf():
                    case '\n':
                        _state = ReaderState.Error;
                        throw new FormatException(); // unexpected newline
                    case '=':
                        SetToken(PlaylistTokenType.AttributeName);
                        _state = ReaderState.AttributeValue;
                        return;
                    default:
                        _cursor++;
                        continue;
                }
            }
        }

        private bool HandlePostLineValue()
        {
            Debug.Assert(_state == ReaderState.PostLineValue);
            Debug.Assert(_buffer[_cursor] == '\n' || _buffer[_cursor] == '\r' && _buffer[_cursor + 1] == '\n');

            _cursor += _buffer[_cursor] == '\r' ? 2 : 1;
            _lineNumber++;
            _lineAnchor = _cursor;

            _state = ReaderState.LineValue;

            return true;
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
