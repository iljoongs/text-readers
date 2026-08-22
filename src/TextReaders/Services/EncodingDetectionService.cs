using System.Text;
using UtfUnknown;

namespace TextReaders.Services;

public sealed class EncodingDetectionService : IEncodingDetectionService
{
    static EncodingDetectionService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public Encoding DetectEncoding(byte[] fileBytes)
    {
        var result = CharsetDetector.DetectFromBytes(fileBytes);
        return result.Detected?.Encoding ?? Encoding.UTF8;
    }
}
