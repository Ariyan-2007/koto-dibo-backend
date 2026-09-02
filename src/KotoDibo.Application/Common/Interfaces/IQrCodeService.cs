namespace KotoDibo.Application.Common.Interfaces;

public interface IQrCodeService
{
    // Returns PNG-encoded bytes of a QR code for the given content (an invite link URL, at present).
    byte[] GeneratePng(string content);
}
