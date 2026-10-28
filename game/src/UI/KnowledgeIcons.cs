using Godot;
using OurLife.Core.Colony;

namespace OurLife.Game.UI;

/// <summary>
/// The pixel icons of the tree of knowledge (<c>assets/icons/knowledge/&lt;id&gt;.png</c>, drawn by
/// <c>tools/make-knowledge-icons.py</c>): one per branch, one per state and the research mark of the
/// HUD. They are loaded once; the text next to them stays as the accessible fallback.
/// </summary>
public static class KnowledgeIcons
{
    private const string Path = "res://assets/icons/knowledge/{0}.png";

    private static readonly Dictionary<string, Texture2D?> Cache = new(StringComparer.Ordinal);

    /// <summary>The icon of a branch of the tree, or null for a branch without one.</summary>
    public static Texture2D? Branch(string branch) => Load("branch_" + branch);

    /// <summary>The icon of the state of a knowledge.</summary>
    public static Texture2D? State(KnowledgeState state) => Load(state switch
    {
        KnowledgeState.Learned => "state_learned",
        KnowledgeState.Queued => "state_queued",
        KnowledgeState.Blocked => "state_blocked",
        _ => "state_available",
    });

    /// <summary>The mark of the research line of the HUD.</summary>
    public static Texture2D? Research() => Load("research");

    /// <summary>A pixel icon as a control: scaled by a whole number, without smoothing.</summary>
    public static TextureRect Rect(Texture2D? texture, int scale = 1) => new()
    {
        Texture = texture,
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        CustomMinimumSize = new Vector2(16 * scale, 16 * scale),
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    private static Texture2D? Load(string id)
    {
        if (!Cache.TryGetValue(id, out var texture))
        {
            var path = string.Format(System.Globalization.CultureInfo.InvariantCulture, Path, id);
            texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            Cache[id] = texture;
        }

        return texture;
    }
}
