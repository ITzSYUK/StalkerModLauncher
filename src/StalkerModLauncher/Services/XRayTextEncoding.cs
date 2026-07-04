using System.Text;

namespace StalkerModLauncher.Services;

internal static class XRayTextEncoding
{
    static XRayTextEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Config = Encoding.GetEncoding(
            1251,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    }

    public static Encoding Config { get; }
}
