namespace Hlks.Quest;

/// <summary>
/// Ablage der App auf der Quest (Kennung je Marke: ch.hkplanung.kicad.quest bzw. ch.hkplanung.xrlabor, Hlks.Xr.Netz.AppMarke):
///   privat  = Context.FilesDir (/data/user/0/&lt;Kennung&gt;/files) – nur die App liest ihn: Kopplung, Zustand, Zwischenspeicher der GLB;
///   Eingang = Context.GetExternalFilesDir(null) (/sdcard/Android/data/&lt;Kennung&gt;/files, AppMarke.Eingang) – dorthin legt P7 per adb die kopplung.json
///             (adb kann dort ohne run-as schreiben); die App übernimmt sie beim Start in den privaten Speicher und löscht sie im Eingang.
/// </summary>
static class Plattform
{
    public static string PrivatOrdner(string? _) => Android.App.Application.Context.FilesDir!.AbsolutePath;

    public static string? EingangOrdner() => Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath;
}
