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
        /// A task that, once awaited, will have a value of <c>true</c> if a token was read; otherwise, <c>false</c>.
        /// </returns>
        public async Task<bool> ReadAsync() => throw new NotImplementedException();

        /// <summary>Asynchronously reads all remaining tokens in the playlist and returns them.</summary>
        /// <returns>
        /// A task that, once awaited, will have a value of a <see cref="IList{T}"/> containing the read tokens in the
        /// order they were read.
        /// </returns>
        public async Task<IList<PlaylistToken>> ReadAllTokensAsync() => throw new NotImplementedException();

        /// <summary>Asynchronously reads the next token in the playlist and returns it.</summary>
        /// <returns>
        /// A task that, once awaited, will have a value of the read token, or <c>null</c> if there are no more tokens
        /// to read.
        /// </returns>
        public async Task<PlaylistToken> ReadTokenAsync() => throw new NotImplementedException();

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

        private async Task<bool> IsEndOfFileAsync()
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
    }
}
