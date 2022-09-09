namespace BlackEye.Connectivity
{
    public interface IDStarFrame
    {
        byte[] Ambe { get; }
        byte[] AmbeAndData { get; }
        byte[] Data { get; }

        bool IsLast();
    }
}