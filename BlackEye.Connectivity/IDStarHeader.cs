namespace BlackEye.Connectivity
{
    public interface IDStarHeader
    {
        string MyCall { get; }
        string Rpt1 { get; }
        string Rpt2 { get; }
        string Suffix { get; }
        string UrCall { get; }
    }
}