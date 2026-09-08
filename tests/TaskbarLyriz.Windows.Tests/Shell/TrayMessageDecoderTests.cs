using TaskbarLyriz.Windows.Shell;

namespace TaskbarLyriz.Windows.Tests.Shell;

[TestClass]
public sealed class TrayMessageDecoderTests
{
    [TestMethod]
    public void GetNotificationCode_ReturnsLowWordForVersionFourMessage()
    {
        var packedMessage = new nint(unchecked((long)0x1234_5678));

        var result = TrayMessageDecoder.GetNotificationCode(packedMessage);

        Assert.AreEqual(0x5678u, result);
    }
}
