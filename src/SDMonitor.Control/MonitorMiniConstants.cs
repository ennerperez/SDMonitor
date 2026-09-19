namespace SDMonitor.Control
{
    public static class MonitorMiniConstants
    {
        public const int VendorId = 0x0FD9;
        public const int ProductId = 0x0063;
        public const int KeyCount = 6;
        public const int Columns = 3;
        public const int Rows = 2;
        public const int KeyImageWidth = 80;
        public const int KeyImageHeight = 80;
        public const int OutputReportLength = 1024;
        public const int FeatureReportLength = 32;
        public const int InputReportLength = 65;
        public const int ImagePayloadOffset = 0x10;
        public const int ImageChunkPayloadLength = OutputReportLength - ImagePayloadOffset;

        public static readonly int[] SupportedProductIds =
        [
            0x0063,
            0x0090,
            0x00B3,
            0x00B8
        ];
    }
}
