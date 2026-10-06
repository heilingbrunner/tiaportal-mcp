using System.IO;
using System.Text;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Re-encodes external source files (*.scl, *.db, *.awl, *.udt) so TIA Portal reads them
    /// correctly.
    ///
    /// TIA Portal reads a source file as UTF-8 only when it starts with a byte order mark;
    /// without one it falls back to the Windows ANSI code page. Most editors save UTF-8 without
    /// a BOM, so a degree sign (UTF-8 bytes C2 B0) arrived in the project as two ANSI characters,
    /// 'A-circumflex' plus the degree sign. Every file is therefore decoded
    /// here first and handed to Openness as a UTF-8 copy with BOM.
    ///
    /// Pure file handling, no Openness - tested without TIA Portal.
    /// </summary>
    public static class SourceFileEncoding
    {
        private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

        /// <summary>
        /// Decodes the bytes of a source file. A BOM (UTF-8, UTF-16 LE/BE) wins; without one the
        /// bytes are tried as strict UTF-8 and, if that fails, read as ANSI - the encoding TIA
        /// Portal itself assumed, which is what older files were written in.
        /// </summary>
        public static string Decode(byte[] bytes)
        {
            if (StartsWith(bytes, Utf8Bom))
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }

            if (StartsWith(bytes, new byte[] { 0xFF, 0xFE }))
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }

            if (StartsWith(bytes, new byte[] { 0xFE, 0xFF }))
            {
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }

            try
            {
                return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Default.GetString(bytes);
            }
        }

        /// <summary>
        /// Writes a UTF-8-with-BOM copy of <paramref name="sourceFile"/> into
        /// <paramref name="targetDirectory"/> and returns its path. The file name and extension
        /// are kept, because TIA Portal tells .scl/.db/.awl/.udt apart by extension.
        /// </summary>
        public static string WriteUtf8BomCopy(string sourceFile, string targetDirectory)
        {
            var text = Decode(File.ReadAllBytes(sourceFile));
            var target = Path.Combine(targetDirectory, Path.GetFileName(sourceFile));

            File.WriteAllText(target, text, new UTF8Encoding(true));

            return target;
        }

        private static bool StartsWith(byte[] bytes, byte[] prefix)
        {
            if (bytes.Length < prefix.Length)
            {
                return false;
            }

            for (var i = 0; i < prefix.Length; i++)
            {
                if (bytes[i] != prefix[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
