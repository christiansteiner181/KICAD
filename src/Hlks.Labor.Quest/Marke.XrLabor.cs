using Hlks.Xr.Netz;

namespace Hlks.Quest;

/// <summary>
/// Marke der Brillen-App «XR-Labor» (XR-Labor L0, entwurf/labor-app.md). Die App übersetzt die Quellen von src/Hlks.Quest ohne deren Marke.Kicad.cs und mit dieser
/// Datei: Name «XR-Labor», Kennung ch.hkplanung.xrlabor, «hallo.marke» = «xrlabor», eigene Ablage am PC. Gekoppelt wird sie mit einem Paket des Labor-PCs
/// (src/Hlks.Labor, Ports 5340/5341 – sie stehen im Paket, nicht hier).
/// </summary>
static partial class Marke
{
    /// <summary>Die Werte der Marke: XR-Labor.</summary>
    public static AppMarke Wert => AppMarke.XrLabor;
}
