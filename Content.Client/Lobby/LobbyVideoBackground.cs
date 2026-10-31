using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client.Lobby;

/// <summary>
/// Plays a frame sequence extracted from a video file as the lobby background.
/// Frames live in a sibling directory next to the source file, e.g. "/Textures/Lobby/video.frames/0001.jpg".
/// Frames are decoded on demand and owned by this object so they can be released when the background changes.
/// </summary>
public sealed class LobbyVideoBackground : IDisposable
{
    public const double FrameRate = 12.0;
    public const string FramesSuffix = ".frames";

    private readonly IResourceCache _cache;
    private readonly ResPath[] _frames;
    private readonly Texture?[] _textures;
    private double _accumulator;
    private int _index;

    public Texture? Texture { get; private set; }

    public int FrameCount => _frames.Length;

    public LobbyVideoBackground(IResourceCache cache, IResourceManager res, ResPath directory)
    {
        _cache = cache;

        var paths = new List<ResPath>();
        foreach (var entry in res.ContentGetDirectoryEntries(directory))
        {
            if (!IsImage(entry))
                continue;

            paths.Add(new ResPath(directory.CanonPath + "/" + entry));
        }

        paths.Sort((a, b) => string.CompareOrdinal(a.CanonPath, b.CanonPath));
        _frames = paths.ToArray();
        _textures = new Texture?[_frames.Length];

        if (_frames.Length > 0)
            Texture = Load(0);
    }

    public void Advance(float deltaSeconds)
    {
        if (_frames.Length <= 1)
            return;

        _accumulator += deltaSeconds;
        var frameTime = 1.0 / FrameRate;
        var advanced = false;

        while (_accumulator >= frameTime)
        {
            _accumulator -= frameTime;
            _index = (_index + 1) % _frames.Length;
            advanced = true;
        }

        if (advanced)
        {
            Texture = Load(_index) ?? Texture;
            Load((_index + 1) % _frames.Length);
        }
    }

    private static bool IsImage(string name)
    {
        var dot = name.LastIndexOf('.');
        if (dot < 0 || dot == name.Length - 1)
            return false;

        var ext = name[(dot + 1)..];
        return ext.Equals("png", StringComparison.OrdinalIgnoreCase)
               || ext.Equals("jpg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals("jpeg", StringComparison.OrdinalIgnoreCase);
    }

    private Texture? Load(int index)
    {
        var cached = _textures[index];
        if (cached != null)
            return cached;

        try
        {
            using var stream = _cache.ContentFileRead(_frames[index]);
            using var image = Image.Load<Rgba32>(stream);
            var texture = Texture.LoadFromImage(image, _frames[index].Filename);
            _textures[index] = texture;
            return texture;
        }
        catch (Exception e)
        {
            Logger.GetSawmill("lobby.video").Error($"Failed to load video frame {_frames[index]}: {e}");
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var texture in _textures)
        {
            if (texture is IDisposable disposable)
                disposable.Dispose();
        }

        Array.Clear(_textures);
        Texture = null;
    }
}
