using Godot;
using OurLife.Core.Sim;

namespace OurLife.Game.Rendering;

/// <summary>
/// Day and night in the view (see "Ciclo visual" in <c>docs/gdd/dia-y-noche.md</c>): the sun
/// crosses the sky with the hour of the game, and the light, the ambient and the sky blend through
/// dawn, day, dusk and night. At night a dim, cold moon takes the sun's place and the ambient
/// keeps the valley readable. Presentation only: it reads the clock and changes nothing. Turned
/// off, the light is the noon of IP1.
/// </summary>
public partial class DayNightCycle : Node
{
    // Minutes of the day when the sun rises and sets; it is highest halfway.
    private const float Sunrise = 6 * 60;
    private const float Sunset = 21 * 60;
    private const float MinutesPerDay = 24 * 60;

    // Highest and lowest angles of the sun over the horizon, in degrees.
    private const float NoonElevation = 62f;
    private const float HorizonElevation = 6f;
    private const float MoonElevation = 45f;

    /// <summary>The look of the scene at one minute of the day; the cycle blends between them.</summary>
    private readonly record struct Look(float Minute, Color Sky, Color Ambient, float AmbientEnergy, Color Light, float LightEnergy);

    // The noon of IP1, also the look when the cycle is off.
    private static readonly Look Day = new(0, new Color(0.62f, 0.76f, 0.86f), new Color(0.85f, 0.88f, 0.95f), 0.35f, Colors.White, 0.9f);
    private static readonly Look Night = new(0, new Color(0.06f, 0.08f, 0.16f), new Color(0.42f, 0.50f, 0.78f), 0.5f, new Color(0.62f, 0.72f, 1f), 0.22f);
    private static readonly Look Dawn = new(0, new Color(0.86f, 0.62f, 0.52f), new Color(0.92f, 0.78f, 0.72f), 0.35f, new Color(1f, 0.72f, 0.5f), 0.5f);
    private static readonly Look Dusk = new(0, new Color(0.82f, 0.48f, 0.38f), new Color(0.88f, 0.66f, 0.6f), 0.32f, new Color(1f, 0.56f, 0.34f), 0.45f);

    // Key moments of the day, by minute; between two of them the look blends.
    private static readonly Look[] Keys =
    [
        Night with { Minute = 0 },
        Night with { Minute = (5 * 60) + 15 },
        Dawn with { Minute = 6 * 60 },
        Day with { Minute = 8 * 60 },
        Day with { Minute = (18 * 60) + 30 },
        Dusk with { Minute = (20 * 60) + 15 },
        Night with { Minute = 21 * 60 },
        Night with { Minute = MinutesPerDay },
    ];

    private Simulation _simulation = null!;
    private DirectionalLight3D _light = null!;
    private Godot.Environment _environment = null!;
    private Func<bool> _enabled = null!;

    /// <param name="enabled">Read every frame, so turning the option on or off shows at once.</param>
    public static DayNightCycle Create(Simulation simulation, DirectionalLight3D light, Godot.Environment environment, Func<bool> enabled)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(light);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(enabled);
        var cycle = new DayNightCycle
        {
            Name = nameof(DayNightCycle),
            _simulation = simulation,
            _light = light,
            _environment = environment,
            _enabled = enabled,
        };
        cycle.Apply();
        return cycle;
    }

    public override void _Process(double delta) => Apply();

    // Releases this reference only; the WorldEnvironment keeps the resource (see MapRenderer.Fill).
    public override void _ExitTree() => _environment.Dispose();

    /// <summary>Puts the light of the current minute of the game, or the noon of IP1 when the cycle is off.</summary>
    public void Apply()
    {
        if (!_enabled())
        {
            Show(Day, new Vector3(-50f, 30f, 0f));
            return;
        }

        var date = _simulation.CurrentDate;
        var minute = (date.Hour * 60f) + date.Minute;
        Show(LookAt(minute), RotationAt(minute));
    }

    private static Look LookAt(float minute)
    {
        for (var i = 1; i < Keys.Length; i++)
        {
            if (minute <= Keys[i].Minute)
            {
                var (from, to) = (Keys[i - 1], Keys[i]);
                var weight = (minute - from.Minute) / (to.Minute - from.Minute);
                return new Look(
                    minute,
                    from.Sky.Lerp(to.Sky, weight),
                    from.Ambient.Lerp(to.Ambient, weight),
                    Mathf.Lerp(from.AmbientEnergy, to.AmbientEnergy, weight),
                    from.Light.Lerp(to.Light, weight),
                    Mathf.Lerp(from.LightEnergy, to.LightEnergy, weight));
            }
        }

        return Keys[^1];
    }

    /// <summary>
    /// By day, the sun goes from the east, low, to high at midday and down to the west. By night
    /// the moon makes the same arc, lower, so the shadows keep moving.
    /// </summary>
    private static Vector3 RotationAt(float minute)
    {
        var day = minute >= Sunrise && minute < Sunset;
        var progress = day
            ? (minute - Sunrise) / (Sunset - Sunrise)
            : ((minute - Sunset + MinutesPerDay) % MinutesPerDay) / (MinutesPerDay - (Sunset - Sunrise));
        var arc = Mathf.Sin(progress * Mathf.Pi);
        var elevation = day ? Mathf.Lerp(HorizonElevation, NoonElevation, arc) : Mathf.Lerp(HorizonElevation, MoonElevation, arc);
        var azimuth = Mathf.Lerp(100f, -100f, progress);
        return new Vector3(-elevation, azimuth, 0f);
    }

    private void Show(Look look, Vector3 rotation)
    {
        _environment.BackgroundColor = look.Sky;
        _environment.AmbientLightColor = look.Ambient;
        _environment.AmbientLightEnergy = look.AmbientEnergy;
        _light.LightColor = look.Light;
        _light.LightEnergy = look.LightEnergy;
        _light.RotationDegrees = rotation;
    }
}
