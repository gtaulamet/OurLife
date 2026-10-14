using Godot;
using OurLife.Core.Sim;

namespace OurLife.Game.Rendering;

/// <summary>
/// Day and night in the view (see "Ciclo visual" in <c>docs/gdd/dia-y-noche.md</c>): the sun
/// crosses the sky with the hour of the game, rising and setting when the night of the colonists
/// ends and starts, and the light, the ambient and the sky blend through
/// dawn, day, dusk and night. At night a dim, cold moon takes the sun's place and the ambient
/// keeps the valley readable. Presentation only: it reads the clock and changes nothing. Turned
/// off, the light is the noon of IP1.
/// </summary>
public partial class DayNightCycle : Node
{
    private const float MinutesPerDay = TimeSettings.MinutesPerDay;

    // Highest and lowest angles of the sun over the horizon, in degrees.
    private const float NoonElevation = 62f;
    private const float HorizonElevation = 6f;
    private const float MoonElevation = 45f;

    // Where the sun rises and sets, in degrees around the vertical axis.
    private const float East = 100f;
    private const float West = -100f;

    /// <summary>The look of the scene at one minute of the day; the cycle blends between them.</summary>
    private readonly record struct Look(float Minute, Color Sky, Color Ambient, float AmbientEnergy, Color Light, float LightEnergy);

    // The noon of IP1, also the look when the cycle is off.
    private static readonly Look Day = new(0, new Color(0.62f, 0.76f, 0.86f), new Color(0.85f, 0.88f, 0.95f), 0.35f, Colors.White, 0.9f);
    private static readonly Look Night = new(0, new Color(0.06f, 0.08f, 0.16f), new Color(0.42f, 0.50f, 0.78f), 0.5f, new Color(0.62f, 0.72f, 1f), 0.22f);
    private static readonly Look Dawn = new(0, new Color(0.86f, 0.62f, 0.52f), new Color(0.92f, 0.78f, 0.72f), 0.35f, new Color(1f, 0.72f, 0.5f), 0.5f);
    private static readonly Look Dusk = new(0, new Color(0.82f, 0.48f, 0.38f), new Color(0.88f, 0.66f, 0.6f), 0.32f, new Color(1f, 0.56f, 0.34f), 0.45f);

    private Simulation _simulation = null!;
    private DirectionalLight3D _light = null!;
    private Godot.Environment _environment = null!;
    private Func<bool> _enabled = null!;

    // Sunrise and sunset are the end and the start of the night the colonists keep.
    private float _sunrise;
    private float _sunset;

    // Key moments of the day by minute, sorted; between two of them, wrapping at midnight, the look blends.
    private Look[] _keys = [];

    // What is shown, to skip frames where neither the minute nor the option changed.
    private (float Minute, bool Enabled)? _shown;

    /// <param name="sunrise">Hour the sun rises: the end of the night of the colonists.</param>
    /// <param name="sunset">Hour the sun sets: the start of their night.</param>
    /// <param name="enabled">Read every frame, so turning the option on or off shows at once.</param>
    public static DayNightCycle Create(
        Simulation simulation, DirectionalLight3D light, Godot.Environment environment, int sunrise, int sunset, Func<bool> enabled)
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
            _sunrise = sunrise * 60f,
            _sunset = sunset * 60f,
        };
        cycle._keys = KeysFor(cycle._sunrise, cycle._sunset);
        cycle.Apply();
        return cycle;
    }

    public override void _Process(double delta) => Apply();

    public override void _Notification(int what)
    {
        // On deletion, not on leaving the tree: the node may leave and enter again. Releases this
        // reference only; the WorldEnvironment keeps the resource (see MapRenderer.Fill).
        if (what == NotificationPredelete)
        {
            _environment.Dispose();
        }
    }

    /// <summary>Puts the light of the current minute of the game, or the noon of IP1 when the cycle is off.</summary>
    public void Apply()
    {
        var enabled = _enabled();
        var date = _simulation.CurrentDate;
        var minute = (date.Hour * 60f) + date.Minute;
        if (_shown == (minute, enabled))
        {
            return;
        }

        _shown = (minute, enabled);
        if (enabled)
        {
            Show(LookAt(minute), RotationAt(minute));
        }
        else
        {
            Show(Day, new Vector3(-50f, 30f, 0f));
        }
    }

    /// <summary>
    /// Night until shortly before sunrise, dawn at sunrise, full day two hours later and until two
    /// and a half hours before sunset, dusk shortly before it, and night from sunset. A short day
    /// or night shrinks the blends, so the keys never cross.
    /// </summary>
    private static Look[] KeysFor(float sunrise, float sunset)
    {
        var day = Forward(sunrise, sunset);
        var night = MinutesPerDay - day;
        var beforeDawn = Math.Min(45f, night / 3);
        var intoDay = Math.Min(120f, day / 3);
        var beforeSunset = Math.Min(150f, day / 3);
        var dusk = beforeSunset * 0.3f;
        return
        [.. new[]
            {
                Night with { Minute = Wrap(sunrise - beforeDawn) },
                Dawn with { Minute = sunrise },
                Day with { Minute = Wrap(sunrise + intoDay) },
                Day with { Minute = Wrap(sunset - beforeSunset) },
                Dusk with { Minute = Wrap(sunset - dusk) },
                Night with { Minute = sunset },
            }
            .OrderBy(key => key.Minute)];
    }

    private static float Wrap(float minute) => ((minute % MinutesPerDay) + MinutesPerDay) % MinutesPerDay;

    /// <summary>Minutes from <paramref name="from"/> forward to <paramref name="to"/>, across midnight if needed.</summary>
    private static float Forward(float from, float to) => Wrap(to - from);

    private Look LookAt(float minute)
    {
        // The last key at or before the minute, wrapping to the last of the day before the first.
        var index = _keys.Length - 1;
        for (var i = 0; i < _keys.Length; i++)
        {
            if (_keys[i].Minute <= minute)
            {
                index = i;
            }
        }

        var (from, to) = (_keys[index], _keys[(index + 1) % _keys.Length]);
        var span = Forward(from.Minute, to.Minute);
        var weight = span == 0 ? 0 : Forward(from.Minute, minute) / span;
        return new Look(
            minute,
            from.Sky.Lerp(to.Sky, weight),
            from.Ambient.Lerp(to.Ambient, weight),
            Mathf.Lerp(from.AmbientEnergy, to.AmbientEnergy, weight),
            from.Light.Lerp(to.Light, weight),
            Mathf.Lerp(from.LightEnergy, to.LightEnergy, weight));
    }

    /// <summary>
    /// By day, the sun goes from the east, low, to high at midday and down to the west. By night
    /// the moon goes back from the west to the east, lower, so the light never jumps.
    /// </summary>
    private Vector3 RotationAt(float minute)
    {
        var dayLength = Forward(_sunrise, _sunset);
        var sinceSunrise = Forward(_sunrise, minute);
        var day = sinceSunrise < dayLength;
        var progress = day ? sinceSunrise / dayLength : (sinceSunrise - dayLength) / (MinutesPerDay - dayLength);
        var arc = Mathf.Sin(progress * Mathf.Pi);
        var elevation = Mathf.Lerp(HorizonElevation, day ? NoonElevation : MoonElevation, arc);
        var azimuth = day ? Mathf.Lerp(East, West, progress) : Mathf.Lerp(West, East, progress);
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
