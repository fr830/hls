namespace SwordsDance.Hls
{
    /// <summary>Specifies HLS playlist token types.</summary>
    public enum PlaylistTokenType
    {
        /// <summary>
        /// Returned by <see cref="PlaylistTextReader.TokenType"/> if data from the underlying playlist has yet to
        /// be read.
        /// </summary>
        None,

        /// <summary>A URI or blank line.</summary>
        Uri,

        /// <summary>A comment marker (<c>#</c>).</summary>
        CommentMarker,

        /// <summary>A comment.</summary>
        Comment,

        /// <summary>A tag name.</summary>
        TagName,

        /// <summary>A tag name/value separator (<c>:</c>).</summary>
        TagNameValueSeparator,

        /// <summary>A tag value.</summary>
        TagValue,

        /// <summary>An attribute name.</summary>
        AttributeName,

        /// <summary>An attribute name/value separator (<c>=</c>).</summary>
        AttributeNameValueSeparator,

        /// <summary>An attribute value.</summary>
        AttributeValue,

        /// <summary>A quoted attribute value marker (<c>"</c>).</summary>
        QuotedAttributeValueMarker,

        /// <summary>A quoted attribute value terminator (<c>"</c>).</summary>
        QuotedAttributeValueTerminator,

        /// <summary>Unexpected post quoted attribute value terminator data.</summary>
        UnexpectedPostQuotedAttributeValueTerminatorData,

        /// <summary>An attribute separator (<c>,</c>).</summary>
        AttributeSeparator,

        /// <summary>A newline-terminated value terminator (LF or CR+LF).</summary>
        NewlineTerminatedValueTerminator,
    }
}
