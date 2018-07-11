using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SwordsDance.Hls
{
    public partial class PlaylistTextReader
    {
        /// <summary>Asynchronously reads the next token in the playlist.</summary>
        /// <returns>
        /// A task that, once awaited, will have a value of <c>true</c> if a token was read and there are more tokens
        /// to read; otherwise, <c>false</c>.
        /// </returns>
        public async Task<bool> ReadAsync()
        {
            InitializeBuffer();

            while (true)
            {
                switch (_state)
                {
                    case ReaderState.LineValue:
                        await ParseLineValueAsync();
                        return true;
                    case ReaderState.TagValueOrAttributeName:
                        await ParseTagValueOrAttributeNameAsync();
                        return true;
                    case ReaderState.AttributeValue:
                        await ParseAttributeValueAsync();
                        return true;
                    case ReaderState.PostQuotedAttributeValue:
                        if (await HandlePostQuotedAttributeValueAsync()) continue;
                        else return false;
                    case ReaderState.AttributeName:
                        await ParseAttributeNameAsync();
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

        /// <summary>A synchronously reads all remaining tokens in the playlist and returns them.</summary>
        /// <returns>
        /// A task that, once awaited, will have a value of a <see cref="IList{T}"/> containing the read tokens in the
        /// order they were read.
        /// </returns>
        public async Task<IList<PlaylistToken>> ReadAllTokensAsync()
        {
            var tokens = new List<PlaylistToken>();
            while (await ReadAsync())
            {
                tokens.Add(new PlaylistToken(TokenType, TokenValue));
            }

            return tokens;
        }

        /// <summary>Asynchronously reads the next token in the playlist and returns it.</summary>
        /// <returns>
        /// A task that, once awaited, will have a value of the read token, or <c>null</c> if there are no more tokens
        /// to read.
        /// </returns>
        public async Task<PlaylistToken> ReadTokenAsync() => await ReadAsync()
            ? new PlaylistToken(TokenType, TokenValue)
            : null;

        private async Task<bool> BufferDataAsync(int lookahead)
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

                _bufferedLength += await _reader.ReadAsync(
                    _buffer,
                    _bufferedLength,
                    bufferLength - _bufferedLength - 1);
            }
            while (_bufferedLength < requiredLength && _bufferedLength != previousBufferedLength);

            _buffer[_bufferedLength] = '\0';

            return _bufferedLength >= requiredLength;
        }

        private async Task<bool> HasLookaheadAsync(int lookahead)
        {
            if (_cursor + lookahead >= _bufferedLength)
            {
                return await BufferDataAsync(lookahead);
            }

            return true;
        }

        private async Task<bool> IsEndOfStreamAsync()
        {
            // must only be called after reading an end of buffered data-marking null character
            Debug.Assert(_cursor == _bufferedLength);
            Debug.Assert(_buffer[_cursor] == '\0');

            return !await BufferDataAsync(lookahead: 0);
        }

        private async Task<bool> IsCrLfAsync()
        {
            // must only be called after reading a carriage return character
            Debug.Assert(_buffer[_cursor] == '\r');

            return await HasLookaheadAsync(1) && _buffer[_cursor + 1] == '\n';
        }

        private async Task ParseLineValueAsync()
        {
            Debug.Assert(_state == ReaderState.LineValue);

            ShiftBuffer();

            while (true)
            {
                switch (_buffer[_cursor])
                {
                    case '\0' when IsEndOfBufferedData():
                        if (await IsEndOfStreamAsync())
                        {
                            // will be handled by the URI parsing procedure
                            goto default;
                        }
                        else continue;
                    case '#':
                        if (await HasLookaheadAsync(3)
                            && _buffer[_cursor + 1] == 'E'
                            && _buffer[_cursor + 2] == 'X'
                            && _buffer[_cursor + 3] == 'T')
                        {
                            await parseTagNameAsync();
                            return;
                        }
                        else
                        {
                            await parseCommentAsync();
                            return;
                        }
                    default:
                        await parseUriOrBlankAsync();
                        return;
                }
            }

            async Task parseTagNameAsync()
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
                            if (await IsEndOfStreamAsync())
                            {
                                SetToken(PlaylistTokenType.TagName);
                                _state = ReaderState.EndOfStream;
                                return;
                            }
                            else continue;
                        case '\r' when await IsCrLfAsync():
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

            async Task parseCommentAsync()
            {
                Debug.Assert(_buffer[_cursor] == '#');

                _valueAnchor = ++_cursor;

                while (true)
                {
                    switch (_buffer[_cursor])
                    {
                        case '\0' when IsEndOfBufferedData():
                            if (await IsEndOfStreamAsync())
                            {
                                SetToken(PlaylistTokenType.Comment);
                                _state = ReaderState.EndOfStream;
                                return;
                            }
                            else continue;
                        case '\r' when await IsCrLfAsync():
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

            async Task parseUriOrBlankAsync()
            {
                _valueAnchor = _cursor;

                while (true)
                {
                    switch (_buffer[_cursor])
                    {
                        case '\0' when IsEndOfBufferedData():
                            if (await IsEndOfStreamAsync())
                            {
                                SetToken(determineTokenType());
                                _state = ReaderState.EndOfStream;
                                return;
                            }
                            else continue;
                        case '\r' when await IsCrLfAsync():
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

        private async Task ParseTagValueOrAttributeNameAsync()
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
                        if (await IsEndOfStreamAsync())
                        {
                            SetToken(PlaylistTokenType.TagValue);
                            _state = ReaderState.EndOfStream;
                            return;
                        }
                        else continue;
                    case '\r' when await IsCrLfAsync():
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

        private async Task ParseAttributeValueAsync()
        {
            Debug.Assert(_state == ReaderState.AttributeValue);
            Debug.Assert(_buffer[_cursor] == '=');

            ShiftBuffer();

            if (await HasLookaheadAsync(1) && _buffer[_cursor + 1] == '"') // parse quoted value
            {
                _cursor += 2;
                _valueAnchor = _cursor;

                while (true)
                {
                    switch (_buffer[_cursor])
                    {
                        case '\0' when IsEndOfBufferedData():
                            if (await IsEndOfStreamAsync())
                            {
                                _state = ReaderState.Error;
                                throw new FormatException(); // unexpected end of stream
                            }
                            else continue;
                        case '\r' when await IsCrLfAsync():
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
                            if (await IsEndOfStreamAsync())
                            {
                                SetToken(PlaylistTokenType.UnquotedAttributeValue);
                                _state = ReaderState.EndOfStream;
                                return;
                            }
                            else continue;
                        case '\r' when await IsCrLfAsync():
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

        private async Task<bool> HandlePostQuotedAttributeValueAsync()
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
                        if (await IsEndOfStreamAsync())
                        {
                            _state = ReaderState.EndOfStream;
                            return false;
                        }
                        else continue;
                    case '\r' when await IsCrLfAsync():
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

        private async Task ParseAttributeNameAsync()
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
                        if (await IsEndOfStreamAsync())
                        {
                            _state = ReaderState.Error;
                            throw new FormatException(); // unexpected end of stream
                        }
                        else continue;
                    case '\r' when await IsCrLfAsync():
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
    }
}
