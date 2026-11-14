namespace GenshinNavigator.Services;

public sealed record MapMatchResult(
    bool Found,
    double X,
    double Y,
    int Inliers,
    double Confidence,
    string Message)
{
    public static MapMatchResult NotFound(string message) => new(false, 0, 0, 0, 0, message);
}
