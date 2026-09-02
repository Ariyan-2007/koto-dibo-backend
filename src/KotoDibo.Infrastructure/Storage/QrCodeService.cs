using KotoDibo.Application.Common.Interfaces;
using QRCoder;

namespace KotoDibo.Infrastructure.Storage;

public class QrCodeService : IQrCodeService
{
    // PngByteQRCode renders without System.Drawing, which keeps this working on the Linux container
    // this API ships in (per the Dockerfile) rather than requiring libgdiplus.
    public byte[] GeneratePng(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(data);
        return pngQrCode.GetGraphic(20);
    }
}
