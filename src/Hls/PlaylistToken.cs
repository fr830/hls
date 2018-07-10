﻿namespace SwordsDance.Hls
{
    /// <summary>Defines an HLS playlist token.</summary>
    public readonly struct PlaylistToken
    {
        /// <summary>
        /// Initializes a new <see cref="PlaylistToken"/> instance with the specified type and value.
        /// </summary>
        /// <param name="type">The type of the token.</param>
        /// <param name="value">The value of the token.</param>
        public PlaylistToken(PlaylistTokenType type, string value)
        {
            Type = type;
            Value = value;
        }

        /// <summary>Gets the type of the token.</summary>
        public PlaylistTokenType Type { get; }

        /// <summary>Gets the value of the token.</summary>
        public string Value { get; }
    }
}