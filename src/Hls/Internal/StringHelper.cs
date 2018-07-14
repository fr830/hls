using System.Text;

namespace SwordsDance.Hls.Internal
{
    internal static class StringHelper
    {
        public static string ToDebugString(string str)
        {
            if (str == null) return "null";

            int length = str.Length;
            var sb = new StringBuilder("\"", length + 4);
            for (int i = 0; i < length; i++)
            {
                char ch = str[i];
                switch (ch)
                {
                    case '"':
                        sb.Append("\\\"");
                        continue;
                    case '\\':
                        sb.Append("\\\\");
                        continue;
                    case '\0':
                        sb.Append("\\0");
                        continue;
                    case '\a':
                        sb.Append("\\a");
                        continue;
                    case '\b':
                        sb.Append("\\b");
                        continue;
                    case '\f':
                        sb.Append("\\f");
                        continue;
                    case '\n':
                        sb.Append("\\n");
                        continue;
                    case '\r':
                        sb.Append("\\r");
                        continue;
                    case '\t':
                        sb.Append("\\t");
                        continue;
                    case '\v':
                        sb.Append("\\v");
                        continue;
                    default:
                        sb.Append(ch);
                        continue;
                }
            }

            return sb.Append('"').ToString();
        }
    }
}
