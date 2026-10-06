using System;
using System.IO;
using System.Linq;
using System.Text;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the re-encoding applied to external source files before TIA Portal reads them.
    /// TIA Portal reads a source without BOM as ANSI, so a degree sign saved as UTF-8 without
    /// BOM showed up garbled. These tests do not connect to TIA Portal.
    /// </summary>
    [TestClass]
    public class Test8SourceFileEncoding
    {
        // "[C" + degree sign + "]", built from its code so this file stays ASCII.
        private static readonly string Text = "[C" + (char)0x00B0 + "]";

        private string _directory = string.Empty;

        [TestInitialize]
        public void Init()
        {
            _directory = Path.Combine(Path.GetTempPath(), "TiaMcpServer.Test", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [TestMethod]
        public void Decode_Utf8WithBom_ReturnsTextWithoutBom()
        {
            var bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(Text)).ToArray();

            Assert.AreEqual(Text, SourceFileEncoding.Decode(bytes));
        }

        [TestMethod]
        public void Decode_Utf8WithoutBom_ReturnsText()
        {
            // The reported case: C2 B0 must not become two ANSI characters.
            var bytes = Encoding.UTF8.GetBytes(Text);

            Assert.AreEqual(Text, SourceFileEncoding.Decode(bytes));
        }

        [TestMethod]
        public void Decode_AnsiDegreeSign_FallsBackToAnsi()
        {
            // A lone B0 is invalid UTF-8; Windows-1252 reads it as the degree sign.
            var bytes = new byte[] { (byte)'[', (byte)'C', 0xB0, (byte)']' };

            var text = SourceFileEncoding.Decode(bytes);

            Assert.AreEqual(Encoding.Default.GetString(bytes), text);

            // Only on Windows-1252 is the expected text known exactly; a DBCS code page may
            // legitimately decode the lone lead byte differently.
            if (Encoding.Default.CodePage == 1252)
            {
                Assert.AreEqual(Text, text);
                Assert.IsFalse(text.Contains((char)0xFFFD), "Invalid UTF-8 must fall back to ANSI, not to replacement characters.");
            }
        }

        [TestMethod]
        public void Decode_EmptyBytes_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, SourceFileEncoding.Decode(new byte[0]));
            Assert.AreEqual(string.Empty, SourceFileEncoding.Decode(new byte[] { 0xEF, 0xBB, 0xBF }));
        }

        [TestMethod]
        public void Decode_Utf16LittleEndianWithBom_ReturnsText()
        {
            var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(Text)).ToArray();

            Assert.AreEqual(Text, SourceFileEncoding.Decode(bytes));
        }

        [TestMethod]
        public void Decode_Utf16BigEndianWithBom_ReturnsText()
        {
            var bytes = Encoding.BigEndianUnicode.GetPreamble().Concat(Encoding.BigEndianUnicode.GetBytes(Text)).ToArray();

            Assert.AreEqual(Text, SourceFileEncoding.Decode(bytes));
        }

        [TestMethod]
        public void Decode_PureAscii_ReturnsUnchanged()
        {
            const string ascii = "TYPE \"UDT_Test\"\r\nEND_TYPE\r\n";

            Assert.AreEqual(ascii, SourceFileEncoding.Decode(Encoding.ASCII.GetBytes(ascii)));
        }

        [TestMethod]
        public void WriteUtf8BomCopy_FileWithoutBom_WritesBomAndKeepsNameAndText()
        {
            var source = Path.Combine(_directory, "UDT_Test.udt");
            var target = Path.Combine(_directory, "copy");

            Directory.CreateDirectory(target);
            File.WriteAllBytes(source, Encoding.UTF8.GetBytes(Text));

            var copy = SourceFileEncoding.WriteUtf8BomCopy(source, target);
            var bytes = File.ReadAllBytes(copy);

            Assert.AreEqual("UDT_Test.udt", Path.GetFileName(copy));
            CollectionAssert.AreEqual(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
            Assert.AreEqual(Text, SourceFileEncoding.Decode(bytes));
        }

        [TestMethod]
        public void WriteUtf8BomCopy_FileWithBom_WritesExactlyOneBom()
        {
            var source = Path.Combine(_directory, "FB_Test.scl");
            var target = Path.Combine(_directory, "copy");
            var original = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(Text)).ToArray();

            Directory.CreateDirectory(target);
            File.WriteAllBytes(source, original);

            var bytes = File.ReadAllBytes(SourceFileEncoding.WriteUtf8BomCopy(source, target));

            CollectionAssert.AreEqual(original, bytes);
        }

        [TestMethod]
        public void WriteUtf8BomCopy_EmptyFile_WritesOnlyBom()
        {
            var source = Path.Combine(_directory, "DB_Empty.db");
            var target = Path.Combine(_directory, "copy");

            Directory.CreateDirectory(target);
            File.WriteAllBytes(source, new byte[0]);

            var bytes = File.ReadAllBytes(SourceFileEncoding.WriteUtf8BomCopy(source, target));

            CollectionAssert.AreEqual(new byte[] { 0xEF, 0xBB, 0xBF }, bytes);
        }
    }
}
