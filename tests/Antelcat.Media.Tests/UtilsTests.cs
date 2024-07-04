using Antelcat.Media.Streams;

namespace Antelcat.Media.Tests;

public class UtilsTests
{
    [Test]
    public void ConstantMemoryStreamTest()
    {
        var stream = new ConstantMemoryStream(64);
        Assert.That(stream.Length, Is.EqualTo(0));
        var buf = Enumerable.Range(0, 64).Select(static i => (byte)i).ToArray();
        stream.Write(buf, 0, 63);
        Assert.That(stream.Length, Is.EqualTo(63));
        stream.Write(buf, 63, 1);
        Assert.That(stream.Length, Is.EqualTo(64));
        Assert.Catch(() => stream.Write(new[] { (byte)64 }));
        Assert.That(stream.Length, Is.EqualTo(64));

        buf = new byte[65];
        Assert.That(stream.Read(buf), Is.EqualTo(64));
        for (var i = 0; i < 64; i++)
        {
            Assert.That(buf[i], Is.EqualTo(i));
        }
        Assert.That(buf[64], Is.EqualTo(0));

        stream.Write(buf, 0, 6);
        Assert.That(stream.Read(buf, 0, 6), Is.EqualTo(6));

        stream.Write(buf, 0, 32);
        Assert.That(stream.Length, Is.EqualTo(32));
        stream.Write(buf, 0, 32);
        Assert.That(stream.Length, Is.EqualTo(64));
        Assert.That(stream.Read(buf, 0, 32), Is.EqualTo(32));
        Assert.That(stream.Length, Is.EqualTo(32));
        Assert.That(stream.Read(buf, 0, 32), Is.EqualTo(32));
        Assert.That(stream.Length, Is.EqualTo(0));
    }
}