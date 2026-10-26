namespace Hlks.Quest;

/// <summary>
/// Ablage der App auf der Quest (H+K XR E1: eine App für alle Werkzeuge, Kennung ch.hkplanung.xr, Hlks.Xr.Netz.XrApp):
///   privat  = Context.FilesDir (/data/user/0/ch.hkplanung.xr/files) – nur die App liest ihn: kopplungen/&lt;werkzeug&gt;.json und je Werkzeug werkzeuge/&lt;kennung&gt;/
///             (Zustand, Zwischenspeicher der GLB, Katalog, wartende Messprotokolle und Marker);
///   Eingang = Context.GetExternalFilesDir(null) (/sdcard/Android/data/ch.hkplanung.xr/files, XrApp.Eingang) – dorthin legt das Werkzeug am PC per adb sein
///             kopplung_&lt;werkzeug&gt;.json (adb kann dort ohne run-as schreiben); die App übernimmt jedes beim Start in den privaten Speicher und löscht es im Eingang.
/// </summary>
static class Plattform
{
    public static string PrivatOrdner(string? _) => Android.App.Application.Context.FilesDir!.AbsolutePath;

    public static string? EingangOrdner() => Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath;
}
