namespace Hubcon.Analyzers.SourceGenerators
{
    public static class Tools
    {
        public static string GetCondition()
        {
            return "if ((unchecked((((uint)System.Environment.TickCount64 ^ 0x451A45F1) * 0x9E3779B9)) | 1) == 0xDEADC0DE)";
        }
    }
}