using OpenCvSharp;
using System.IO;

namespace GenshinNavigator.Services;

/// <summary>Зіставляє видиму мінікарту з локально вибраним зображенням карти.</summary>
public sealed class MinimapLocator : IDisposable
{
    private readonly object _referenceLock = new();
    private string? _cachedPath;
    private DateTime _cachedWriteTimeUtc;
    private Mat? _referenceDescriptors;
    private KeyPoint[] _referenceKeypoints = [];
    private Size _referenceSize;
    private int _referenceOffsetX;
    private int _referenceOffsetY;
    private ORB? _orb;

    public MapMatchResult Locate(Mat minimap, string referencePath)
    {
        if (string.IsNullOrWhiteSpace(referencePath) || !File.Exists(referencePath))
            return MapMatchResult.NotFound("Оберіть скриншот карти в налаштуваннях.");

        try
        {
            EnsureReference(referencePath);
            if (_referenceDescriptors is null || _referenceDescriptors.Empty())
                return MapMatchResult.NotFound("Не вдалося зчитати скриншот карти.");

            using var query = ToMaskedGray(minimap, maskCenter: true);
            using var queryDescriptors = new Mat();
            var orb = GetOrb();
            orb.DetectAndCompute(query, null, out var queryKeypoints, queryDescriptors);
            if (queryKeypoints.Length < 12 || queryDescriptors.Empty())
                return MapMatchResult.NotFound($"На мінікарті знайдено лише {queryKeypoints.Length} ключових точок.");

            using var matcher = new BFMatcher(NormTypes.Hamming, crossCheck: false);
            var pairs = matcher.KnnMatch(queryDescriptors, _referenceDescriptors, k: 2);
            var good = pairs
                .Where(pair => pair.Length == 2 && pair[0].Distance < 0.78f * pair[1].Distance)
                .Select(pair => pair[0])
                .ToArray();
            if (good.Length < 10)
                return MapMatchResult.NotFound($"Знайшлося {good.Length} попередніх збігів; потрібно щонайменше 10. Перевірте область і масштаб скриншота карти.");

            var refPoints = good.Select(match =>
            {
                var point = _referenceKeypoints[match.TrainIdx].Pt;
                return new Point2d(point.X, point.Y);
            }).ToArray();
            var queryPoints = good.Select(match =>
            {
                var point = queryKeypoints[match.QueryIdx].Pt;
                return new Point2d(point.X, point.Y);
            }).ToArray();
            using var inlierMask = new Mat();
            using var homography = Cv2.FindHomography(
                refPoints, queryPoints, HomographyMethods.Ransac, 5.5, inlierMask);
            if (homography.Empty())
                return MapMatchResult.NotFound($"Геометрія не підтвердила збіг ({good.Length} кандидатів). Спробуйте чіткіший скриншот карти.");

            var inliers = 0;
            for (var i = 0; i < good.Length; i++)
                if (inlierMask.At<byte>(i) != 0) inliers++;
            var inlierRatio = (double)inliers / good.Length;
            if (inliers < 8 || inlierRatio < 0.2)
                return MapMatchResult.NotFound($"Збіг не пройшов перевірку: {inliers} із {good.Length} точок узгодилися. Потрібні щонайменше 8 точок і 20% узгодження.");

            using var queryToMap = homography.Inv();
            // У мінікарті маркер персонажа розташований трохи правіше й нижче геометричного центра.
            var playerAnchor = new Point2f(minimap.Width * 0.63f, minimap.Height * 0.58f);
            var mapCenter = Cv2.PerspectiveTransform([playerAnchor], queryToMap)[0];
            mapCenter.X += _referenceOffsetX;
            mapCenter.Y += _referenceOffsetY;
            if (mapCenter.X < 0 || mapCenter.Y < 0 || mapCenter.X >= _referenceSize.Width || mapCenter.Y >= _referenceSize.Height)
                return MapMatchResult.NotFound("Знайдений фрагмент лежить за межами зображення карти.");

            var confidence = Math.Clamp(inlierRatio, 0, 1);
            return new MapMatchResult(true, mapCenter.X, mapCenter.Y, inliers, confidence,
                $"Збіг знайдено · {inliers} точок · {confidence:P0}");
        }
        catch (Exception ex) when (ex is OpenCVException or IOException or UnauthorizedAccessException or ArgumentException or DllNotFoundException)
        {
            return MapMatchResult.NotFound("Помилка аналізу зображення карти.");
        }
    }

    private void EnsureReference(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var writeTime = File.GetLastWriteTimeUtc(fullPath);
        lock (_referenceLock)
        {
            if (_referenceDescriptors is not null && _cachedPath == fullPath && _cachedWriteTimeUtc == writeTime) return;
            _referenceDescriptors?.Dispose();
            _referenceDescriptors = null;

            using var fullReference = Cv2.ImRead(fullPath, ImreadModes.Color);
            if (fullReference.Empty()) return;
            _referenceSize = fullReference.Size();
            // У повноекранного скриншота прибираємо ліву панель гри, залишаючи карту й маркери.
            _referenceOffsetX = fullReference.Width >= 1200 ? (int)(fullReference.Width * 0.19) : 0;
            _referenceOffsetY = 0;
            using var reference = _referenceOffsetX == 0
                ? fullReference.Clone()
                : new Mat(fullReference, new Rect(_referenceOffsetX, 0, fullReference.Width - _referenceOffsetX, fullReference.Height));
            using var gray = ToMaskedGray(reference, maskCenter: false);
            var descriptors = new Mat();
            GetOrb().DetectAndCompute(gray, null, out _referenceKeypoints, descriptors);
            _referenceDescriptors = descriptors;
            _cachedPath = fullPath;
            _cachedWriteTimeUtc = writeTime;
        }
    }

    private static Mat ToMaskedGray(Mat image, bool maskCenter)
    {
        var gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        if (!maskCenter) return gray;

        using var mask = new Mat(image.Size(), MatType.CV_8UC1, Scalar.All(0));
        var center = new Point(image.Width / 2, image.Height / 2);
        Cv2.Circle(mask, center, Math.Min(image.Width, image.Height) / 2 - 4, Scalar.All(255), -1);
        // Стрілка персонажа на інтерфейсі стоїть з невеликим зсувом від центру мінікарти.
        var playerAnchor = new Point((int)(image.Width * 0.63), (int)(image.Height * 0.58));
        Cv2.Circle(mask, playerAnchor, Math.Min(image.Width, image.Height) / 11, Scalar.All(0), -1);
        Cv2.BitwiseAnd(gray, gray, gray, mask);
        return gray;
    }

    // Більше рівнів масштабу важливо: у мінікарті та скриншоті світу одна й та сама
    // місцевість може мати суттєво різний розмір.
    private ORB GetOrb() => _orb ??= ORB.Create(nFeatures: 6000, scaleFactor: 1.2f, nLevels: 12);

    public void Dispose()
    {
        _referenceDescriptors?.Dispose();
        _orb?.Dispose();
    }
}
