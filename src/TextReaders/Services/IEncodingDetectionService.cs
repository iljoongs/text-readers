using System.Text;

namespace TextReaders.Services;

public interface IEncodingDetectionService
{
    Encoding DetectEncoding(byte[] fileBytes);
}
