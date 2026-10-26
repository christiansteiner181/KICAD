// «Quest App installieren/aktualisieren» (Modul XR, Quest App P7; Entscheid Christian 23.10.2026: «KICAD installiert die App» per adb): der adb-Teil des Dialogs
// «Quest verbinden». Ein Teil von QuestDienst (gleiche Sperre, gleiche adb-Suche, gleiche Zeitgrenzen), damit nie zwei adb-Abläufe zugleich laufen.
// Was KICAD tut, in dieser Reihenfolge (macht dasselbe wie werkzeuge/quest_brillentest.ps1, nur mit Knopf und ohne Server-Start):
//   1. Voraussetzungen: Netzzugang dieses PCs ist an (Einstellung je PC, XrNetzDienst), die abgelegte App ist heil (version.json passt zur APK).
//   2. Brille wählen (freigegeben, «device»): bei «über Kabel» bevorzugt der USB-Weg, bei «über WLAN» der WLAN-Weg. Modell und Seriennummer lesen (getprop).
//   3. «adb install -r» der APK aus der Ablage (Vorgabe %USERPROFILE%\HLKS_CAD_werkzeuge\hk_xr, Einstellung quest.app_ablage; gebaut von werkzeuge/quest_app_bereitstellen.ps1).
//   4. Nur «über Kabel»: «adb reverse» des HTTPS-Ports des Netzzugangs (Vorgabe 5140) und 127.0.0.1 als erste Adresse im Paket.
//   5. Kopplungspaket (Weg A, Fassung 2, mit Schlüssel und Werkzeug) erzeugen und mit «adb push» in den Eingang der App legen
//      (/sdcard/Android/data/ch.hkplanung.xr/files/kopplung_<werkzeug>.json; die App übernimmt und löscht es). Das Paket liegt am PC nur für die Dauer von «adb push»
//      in einer Datei (eigener Ordner unter %TEMP%, danach überschrieben und gelöscht), steht in keiner Antwort und in keinem Protokoll. Scheitert die Übertragung,
//      wird die frisch angelegte Kopplung widerrufen (kein Schlüssel ohne Brille).
//   6. App starten (resolve-activity, am start -n). Scheitert nur der Start, gilt die Installation trotzdem (Meldung: von Hand starten).
// SICHERHEIT wie beim Quest-Verbinder: Es startet nur adb.exe, ohne Shell, mit festen Argumenten. Eingesetzt werden nur die Seriennummer aus «adb devices», der
// Pfad der geprüften APK, ein Port aus dem Netzzugang und feste Texte. Reine Rechner-Funktion, für die KI verboten (KiEinordnung.RechnerFunktionen «quest_app»).
// APP-MARKE (XR-Labor L0, entwurf/labor-app.md; H+K XR E1, entwurf/xr-empfaenger.md): Welche App installiert wird (Paket, Eingang, Ablage, Einstellungsschlüssel, Name in Meldungen),
// steht in QuestEinstellungen.AppMarke. Seit E1 gibt es EINE App «H+K XR» (ch.hkplanung.xr) für alle Werkzeuge: Die Marken von KICAD (QuestAppMarke.Kicad) und XR-Labor
// (src/Hlks.Labor, LaborMarke.App) zeigen auf dieselbe App und dieselbe Ablage (%USERPROFILE%\HLKS_CAD_werkzeuge\hk_xr, Einstellung quest.app_ablage) und unterscheiden sich nur
// im Werkzeug (Kennung kicad | xrlabor, Name KICAD | XR-Labor; daraus der Name der Eingangsdatei kopplung_<werkzeug>.json und die Angaben im Kopplungspaket). Die Marke ist
// fest im Code, nie eine Eingabe; Paket, Werkzeug und Ordnername werden trotzdem geprüft, weil sie in adb-Argumente und Dateinamen eingesetzt werden.
// ALTE APPS (E1): Die bisherigen Apps «KICAD» (ch.hkplanung.kicad.quest) und «XR-Labor» (ch.hkplanung.xrlabor) sind durch «H+K XR» ersetzt. Der Zustand nennt die auf der Brille
// noch installierten (brille.alteApps), «Alte Apps entfernen» (AlteAppsEntfernen) deinstalliert genau diese beiden Pakete per «adb uninstall» – nur auf Knopfdruck, nie sonst.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;

namespace Hlks.Web;

/// <summary>
/// Die Brillen-App, die QuestDienst installiert und koppelt, und das Werkzeug, mit dem sie sich koppelt. Paket = Android-Paketname (App-Kennung, seit E1 für alle Werkzeuge
/// ch.hkplanung.xr); Name = Name der App in der Brille (Meldungen, «H+K XR»); AblageSchluessel = Benutzereinstellung mit dem Ordner der abgelegten App;
/// AblageOrdner = Ordnername der Vorgabe unter %USERPROFILE%\HLKS_CAD_werkzeuge; Bereitstellen = Befehl, der die APK baut und ablegt (nur für Meldungen);
/// Werkzeug = Kennung des Werkzeugs (kicad | xrlabor; Muster [a-z][a-z0-9_-]{0,31}) für das Kopplungspaket und den Namen der Eingangsdatei; WerkzeugName = Name des Werkzeugs
/// am PC in Meldungen und im Paket («KICAD», «XR-Labor»).
/// </summary>
public sealed record QuestAppMarke(string Paket, string Name, string AblageSchluessel, string AblageOrdner, string Bereitstellen, string Werkzeug = "kicad", string WerkzeugName = "KICAD")
{
    static readonly Regex PaketMuster = new(@"^[a-z][a-z0-9_]{0,30}(\.[a-z][a-z0-9_]{0,30}){1,5}$", RegexOptions.CultureInvariant);
    static readonly Regex OrdnerMuster = new(@"^[A-Za-z0-9_]{1,40}$", RegexOptions.CultureInvariant);
    static readonly Regex SchluesselMuster = new(@"^[a-z][a-z0-9_.\-]{0,63}$", RegexOptions.CultureInvariant);
    static readonly Regex WerkzeugMuster = new(@"^[a-z][a-z0-9_\-]{0,31}$", RegexOptions.CultureInvariant);

    public string Paket { get; } = PaketMuster.IsMatch(Paket ?? "") ? Paket! : throw new ArgumentException($"Ungültiger Paketname «{Paket}».", nameof(Paket));
    public string AblageOrdner { get; } = OrdnerMuster.IsMatch(AblageOrdner ?? "") ? AblageOrdner! : throw new ArgumentException($"Ungültiger Ordnername «{AblageOrdner}».", nameof(AblageOrdner));
    public string AblageSchluessel { get; } = SchluesselMuster.IsMatch(AblageSchluessel ?? "") ? AblageSchluessel! : throw new ArgumentException($"Ungültiger Schlüssel «{AblageSchluessel}».", nameof(AblageSchluessel));
    public string Werkzeug { get; } = WerkzeugMuster.IsMatch(Werkzeug ?? "") ? Werkzeug! : throw new ArgumentException($"Ungültige Werkzeugkennung «{Werkzeug}».", nameof(Werkzeug));
    public string WerkzeugName { get; } = !string.IsNullOrWhiteSpace(WerkzeugName) && WerkzeugName.Length <= 40 && !WerkzeugName.Any(char.IsControl) ? WerkzeugName.Trim()
        : throw new ArgumentException($"Ungültiger Werkzeugname «{WerkzeugName}».", nameof(WerkzeugName));

    /// <summary>Eingang der App auf der Brille: dorthin legt adb das Kopplungspaket.</summary>
    public string Eingang => "/sdcard/Android/data/" + Paket + "/files";

    /// <summary>Name der Eingangsdatei dieses Werkzeugs: kopplung_&lt;werkzeug&gt;.json (je Werkzeug ein Paket; die App übernimmt jedes und löscht es im Eingang).</summary>
    public string KopplungDatei => "kopplung_" + Werkzeug + ".json";

    /// <summary>Das Kopplungspaket dieses Werkzeugs im Eingang der App (Weg A, adb push).</summary>
    public string KopplungEingang => Eingang + "/" + KopplungDatei;

    /// <summary>KICAD: die App «H+K XR» (ch.hkplanung.xr), Einstellung quest.app_ablage, Ablage %USERPROFILE%\HLKS_CAD_werkzeuge\hk_xr, Werkzeug kicad / «KICAD».</summary>
    public static QuestAppMarke Kicad { get; } = new(QuestDienst.AppPaket, QuestDienst.AppName, QuestDienst.AppAblageSchluessel, QuestDienst.AppOrdner, @"werkzeuge\quest_app_bereitstellen.ps1", "kicad", "KICAD");
}

/// <summary>Eine alte Brillen-App, die «H+K XR» ersetzt (Paketname und Name der App von früher).</summary>
public sealed record QuestAlteApp(string Paket, string Name);

/// <summary>Die abgelegte App: Inhalt von version.json (geschrieben von werkzeuge/quest_app_bereitstellen.ps1), geprüft gegen die APK.</summary>
public sealed record QuestAppVersion(string Datei, string? Commit, string? Zeit, long Groesse, string Sha256, string? VersionName, int? VersionCode);

/// <summary>Ablage der App am PC. Fehler: warum sie nicht benutzbar ist (null = heil).</summary>
public sealed record QuestAppAblage(string Ordner, string Quelle, bool Vorhanden, QuestAppVersion? Version, string? ApkPfad, string? Fehler);

/// <summary>Die in der Brille installierte Fassung (aus «dumpsys package»).</summary>
public sealed record QuestAppInstalliert(bool Installiert, string? VersionName, int? VersionCode, string? Aktualisiert, string? Erstinstalliert);

/// <summary>Ein Schritt von «App installieren» für die Anzeige. Ok = false: hier ist es gescheitert (oder eine Warnung beim letzten Schritt).</summary>
public sealed record QuestAppSchritt(string Id, string Text, bool Ok);

/// <summary>Ergebnis von «App installieren/aktualisieren». Nie ein Schlüssel, nie das Paket.</summary>
public sealed record QuestAppErgebnis(bool Ok, IReadOnlyList<QuestAppSchritt> Schritte, string? Meldung, string? Fehler, string? Vorschlag, string? Weg, string? Brille, string? Name);

public sealed partial class QuestDienst
{
    /// <summary>Paketname (Kennung) der KICAD-App; fest, nie aus einer Eingabe. Welche App dieser Dienst installiert, sagt Marke (QuestEinstellungen.AppMarke).</summary>
    public const string AppPaket = "ch.hkplanung.kicad.quest";
    /// <summary>Schlüssel der Benutzereinstellung mit dem Ordner der abgelegten KICAD-App (Vorgabe: %USERPROFILE%\HLKS_CAD_werkzeuge\quest_app).</summary>
    public const string AppAblageSchluessel = "quest.app_ablage";
    /// <summary>Eingang der KICAD-App auf der Brille: dorthin legt adb das Paket (die App übernimmt es in ihren privaten Speicher und löscht es hier).</summary>
    public const string AppEingang = "/sdcard/Android/data/" + AppPaket + "/files";
    public const string AppVersionDatei = "version.json";

    /// <summary>Die App dieses Dienstes (Vorgabe KICAD; das XR-Labor gibt seine eigene mit).</summary>
    public QuestAppMarke Marke => einstellungen.AppMarke;

    /// <summary>Wird von MapQuestApp gesetzt: Besteht für diese Kennung der Brille eine Kopplung am Netzzugang? Ohne Setzen gilt «nein».</summary>
    public Func<string, bool>? KopplungBesteht { get; set; }
    /// <summary>Wird von MapQuestApp gesetzt: Ist der Netzzugang dieses PCs aktiv?</summary>
    public Func<bool>? ZugangAktiv { get; set; }

    static readonly Regex AppDateiMuster = new(@"^[A-Za-z0-9][A-Za-z0-9._\-]{0,100}\.apk$", RegexOptions.CultureInvariant);
    static readonly Regex Hex64 = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    static readonly Regex KurzText = new(@"^[A-Za-z0-9._:+\- ]{1,40}$", RegexOptions.CultureInvariant);
    Regex? aktivitaetMuster;
    /// <summary>Startaktivität der App aus «resolve-activity»: «&lt;Paket der Marke&gt;/&lt;Klasse&gt;».</summary>
    Regex AktivitaetMuster => aktivitaetMuster ??= new(@"^" + Regex.Escape(Marke.Paket) + @"/[A-Za-z0-9_.$]{1,200}$", RegexOptions.CultureInvariant);

    // ---------------------------------------------------------------- Ablage (rein bis auf das Lesen der Dateien)

    /// <summary>Meldung, wenn der Ordner der Ablage nicht benutzt werden darf (vollständiger lokaler Pfad, kein Netzpfad, kein «..»); sonst null.</summary>
    public static string? AppAblageFehler(string? pfad)
    {
        if (string.IsNullOrWhiteSpace(pfad)) return "Der Ordner ist leer. Zum Zurücksetzen die Einstellung leeren (zurücksetzen).";
        var s = pfad.Trim();
        if (s.Length > 240) return "Der Pfad ist zu lang (höchstens 240 Zeichen).";
        if (s.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || s.Contains('"')) return "Der Pfad enthält ungültige Zeichen.";
        if (s.StartsWith(@"\\", StringComparison.Ordinal) || s.StartsWith("//", StringComparison.Ordinal))
            return "Netzpfade sind nicht erlaubt: die App muss auf diesem Rechner liegen.";
        if (!Path.IsPathRooted(s) || !Path.IsPathFullyQualified(s)) return @"Der Pfad muss vollständig sein, z. B. C:\Users\...\HLKS_CAD_werkzeuge\quest_app.";
        if (s.Split(['\\', '/']).Any(t => t == "..")) return "Der Pfad darf kein «..» enthalten.";
        return null;
    }

    /// <summary>Prüfung der Einstellung «quest.app_ablage» (BenutzerEinstellungen.Bekannt).</summary>
    public static string? AppAblagePruefen(JsonNode w) =>
        w is JsonValue v && v.TryGetValue<string>(out var s) ? AppAblageFehler(s) : @"Der Wert muss ein Ordnerpfad (Text) sein, z. B. C:\Users\...\HLKS_CAD_werkzeuge\quest_app.";

    /// <summary>Der Ordner der abgelegten App und woher er stammt (einstellung | vorgabe). Einstellung und Vorgabe-Ordner kommen aus der Marke.</summary>
    public (string Ordner, string Quelle) AppOrdner()
    {
        if (benutzer.Hole(Marke.AblageSchluessel) is JsonValue v && v.TryGetValue<string>(out var p) && !string.IsNullOrWhiteSpace(p) && AppAblageFehler(p) is null)
            return (Path.GetFullPath(p.Trim()), "einstellung");
        if (!string.IsNullOrWhiteSpace(einstellungen.AppAblage)) return (einstellungen.AppAblage!, "vorgabe");
        var profil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return (Path.Combine(profil, "HLKS_CAD_werkzeuge", Marke.AblageOrdner), "vorgabe");
    }

    /// <summary>Liest version.json und prüft die APK dagegen (Datei da, Grösse, SHA-256). Wirft nie; Fehler stehen in der Antwort.</summary>
    public QuestAppAblage AppAblageLesen()
    {
        var (ordner, quelle) = AppOrdner();
        var vdatei = Path.Combine(ordner, AppVersionDatei);
        if (!File.Exists(vdatei))
            return new QuestAppAblage(ordner, quelle, false, null, null, $"In {ordner} liegt keine App (version.json fehlt). Mit {Marke.Bereitstellen} bauen und ablegen.");
        QuestAppVersion v;
        try { v = VersionLesen(File.ReadAllText(vdatei, Encoding.UTF8)); }
        catch (Exception ex) when (ex is HlksFehler or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return new QuestAppAblage(ordner, quelle, true, null, null, "version.json ist unbrauchbar: " + ex.Message);
        }
        var apk = Path.Combine(ordner, v.Datei);
        if (!File.Exists(apk)) return new QuestAppAblage(ordner, quelle, true, v, null, $"Die Datei {v.Datei} fehlt in {ordner}. Mit {Marke.Bereitstellen} neu ablegen.");
        try
        {
            var laenge = new FileInfo(apk).Length;
            if (laenge != v.Groesse)
                return new QuestAppAblage(ordner, quelle, true, v, null, $"Die Grösse von {v.Datei} ({laenge} Byte) passt nicht zu version.json ({v.Groesse} Byte). Neu ablegen.");
            string hash;
            using (var s = File.OpenRead(apk)) hash = Convert.ToHexStringLower(SHA256.HashData(s));
            if (!string.Equals(hash, v.Sha256, StringComparison.Ordinal))
                return new QuestAppAblage(ordner, quelle, true, v, null, $"Die Prüfsumme von {v.Datei} passt nicht zu version.json (Datei verändert oder beschädigt). Neu ablegen.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new QuestAppAblage(ordner, quelle, true, v, null, $"{v.Datei} ist nicht lesbar: {ex.Message}");
        }
        return new QuestAppAblage(ordner, quelle, true, v, apk, null);
    }

    /// <summary>Liest den Inhalt von version.json: { datei, commit?, zeit?, groesse, sha256, versionName?, versionCode? }. Wirft HlksFehler bei jeder Abweichung.</summary>
    public static QuestAppVersion VersionLesen(string text)
    {
        JsonObject o;
        try { o = JsonNode.Parse(text.TrimStart('\uFEFF'), documentOptions: new System.Text.Json.JsonDocumentOptions { MaxDepth = 8 }) as JsonObject ?? throw new HlksFehler("kein JSON-Objekt"); }
        catch (System.Text.Json.JsonException) { throw new HlksFehler("kein gültiges JSON"); }
        var datei = TextVon(o["datei"]);
        if (datei is null || !AppDateiMuster.IsMatch(datei)) throw new HlksFehler("«datei» fehlt oder ist kein Dateiname einer APK");
        if (o["groesse"] is not JsonValue gv || !gv.TryGetValue<long>(out var groesse) || groesse <= 0 || groesse > 2_000_000_000) throw new HlksFehler("«groesse» fehlt oder ist ungültig");
        var sha = TextVon(o["sha256"])?.ToLowerInvariant();
        if (sha is null || !Hex64.IsMatch(sha)) throw new HlksFehler("«sha256» fehlt oder ist ungültig");
        string? Kurz(string name) => TextVon(o[name]) is { } t && KurzText.IsMatch(t) ? t : null;
        int? code = o["versionCode"] is JsonValue cv && cv.TryGetValue<int>(out var c) && c > 0 ? c : null;
        return new QuestAppVersion(datei, Kurz("commit"), Kurz("zeit"), groesse, sha, Kurz("versionName"), code);
    }

    // ---------------------------------------------------------------- Weg der App (je PC) und Weiterleitung

    /// <summary>Der gemerkte Weg der Installation: Kennung der Brille, «kabel» oder «wlan», Port des Netzzugangs.</summary>
    public sealed record AppWegStand(string Kennung, string Weg, int Port);

    string AppWegPfad()
    {
        if (!string.IsNullOrWhiteSpace(einstellungen.AppWegDatei)) return einstellungen.AppWegDatei!;
        // im Testlauf je Prozess eine eigene Datei: gleichzeitige Testläufe (z. B. parallele Agenten) teilten sonst den gemerkten Weg (Befund 24.10.2026)
        if (FamilienBibliothekDienst.Testlauf()) return Path.Combine(Path.GetTempPath(), "HLKS_CAD", "quest_app_weg_test", Environment.ProcessId.ToString(), "weg.json");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HLKS_CAD", "xr", "quest_app_weg.json");
    }

    /// <summary>Liest den gemerkten Weg; null, wenn es ihn nicht gibt oder er unbrauchbar ist. Wirft nie.</summary>
    public AppWegStand? AppWegLesen()
    {
        try
        {
            var p = AppWegPfad();
            if (!File.Exists(p)) return null;
            if (JsonNode.Parse(File.ReadAllText(p, Encoding.UTF8)) is not JsonObject o) return null;
            var kennung = TextVon(o["kennung"]);
            var weg = TextVon(o["weg"]);
            if (kennung is null || !SerieMuster.IsMatch(kennung) || weg is not ("kabel" or "wlan")) return null;
            return o["port"] is JsonValue pv && pv.TryGetValue<int>(out var port) && port is >= 1 and <= 65535 ? new AppWegStand(kennung, weg, port) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException) { return null; }
    }

    void AppWegSchreiben(AppWegStand s)
    {
        try
        {
            var p = AppWegPfad();
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, new JsonObject { ["kennung"] = s.Kennung, ["weg"] = s.Weg, ["port"] = s.Port }.ToJsonString(), new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* nur eine Zugabe: ohne die Datei gilt der Browser-Weg wie bisher */ }
    }

    /// <summary>Ist die Quest App im Kabelweg gekoppelt (zuletzt per Kabel installiert, die Kopplung besteht noch)? Dann darf nichts adb in der Brille neu starten (adb tcpip) und die
    /// Weiterleitung tcp:&lt;Port&gt; muss nach jeder Änderung der adb-Verbindung wieder da sein. Ruft kein adb.</summary>
    public bool AppKabelAktiv(out int port)
    {
        port = 0;
        var s = AppWegLesen();
        if (s is null || s.Weg != "kabel" || KopplungBesteht?.Invoke(s.Kennung) != true) return false;
        port = s.Port;
        return true;
    }

    /// <summary>Stellt die Weiterleitung der Quest App (adb reverse tcp:&lt;Port&gt;) wieder her, wenn die App im Kabelweg gekoppelt ist und adb sie verloren hat (Brillentest 23.10.2026:
    /// nach «adb tcpip» war sie weg, die App getrennt). Gewählt wird der USB-Weg, sonst der WLAN-Weg; hat einer sie schon, geschieht nichts. Nur unter der Sperre aufrufen; scheitert still.</summary>
    internal async Task AppWeiterleitungSicher(AdbFund adb)
    {
        try
        {
            if (!AppKabelAktiv(out var port)) return;
            var wege = (await GeraeteListe(adb)).Where(g => g.Zustand == "device").OrderBy(g => g.Wlan ? 1 : 0).ToList();
            if (wege.Count == 0) return;
            foreach (var g in wege) if (await HatWeiterleitung(adb, g.Seriennummer, port)) return;
            await WeiterleitungSetzen(adb, wege[0].Seriennummer, port, wiederholen: true);
        }
        catch (HlksFehler) { /* die Hauptsache (Verbinden) geht vor; der nächste Schritt versucht es wieder */ }
    }

    /// <summary>VR-Auftrag ohne verbundene App: Hat KICAD die App installiert und gekoppelt (gemerkter Weg, bestehende Kopplung) und ist der Netzzugang an, wird sie gestartet (am start, wie bei der Installation) – der Auftrag
    /// kommt über den Kanal, sobald sie verbindet (willkommen.auftrag). true = App gestartet (dann startet KICAD keinen Browser); false = nicht möglich (keine App, keine Kopplung, kein adb …).</summary>
    public async Task<bool> AppFuerAuftragStarten()
    {
        // Ohne den gemerkten Weg (KICAD hat die App nie installiert und gekoppelt), ohne Kopplung oder bei ausgeschaltetem Netzzugang gibt es nichts zu starten – und kein adb-Aufruf.
        if (ZugangAktiv?.Invoke() != true) return false;
        var stand = AppWegLesen();
        if (stand is null || KopplungBesteht?.Invoke(stand.Kennung) != true) return false;   // widerrufen: die App würde «neu koppeln» melden und nicht verbinden
        await sperre.WaitAsync();
        try
        {
            var adb = AdbFinden(out _);
            if (adb is null) return false;
            List<QuestGeraet> geraete;
            try { geraete = await GeraeteListe(adb); }
            catch (HlksFehler) { return false; }
            var g = AppGeraetWaehlen(geraete, stand.Weg);
            if (g is null) return false;
            await AppWeiterleitungSicher(adb);
            return await AppStarten(adb, g.Seriennummer) is null;   // ist die App inzwischen deinstalliert, scheitert der Start: dann gilt der Browser-Weg
        }
        finally { sperre.Release(); }
    }
    /// <summary>Kennung der Brille für die Kopplung: ro.serialno (auf USB und WLAN gleich), sonst die Seriennummer von adb.</summary>
    async Task<string> KennungVon(AdbFund adb, string serie)
    {
        var r = await Aufruf(adb, "-s", serie, "shell", "getprop", "ro.serialno");
        if (!r.Zeitueberschreitung && r.Code == 0)
        {
            try { return XrKopplungen.BrillePruefen(r.Ausgabe); }
            catch (HlksFehler) { /* unbrauchbar */ }
        }
        return serie;
    }

    // ---------------------------------------------------------------- installierte Fassung (rein)

    static readonly Regex VersionNameMuster = new(@"\bversionName=([A-Za-z0-9._+\-]{1,30})", RegexOptions.CultureInvariant);
    static readonly Regex VersionCodeMuster = new(@"\bversionCode=(\d{1,10})\b", RegexOptions.CultureInvariant);
    static readonly Regex AktualisiertMuster = new(@"\blastUpdateTime=([0-9][0-9:\- TZ.+]{5,30})", RegexOptions.CultureInvariant);
    static readonly Regex ErstMuster = new(@"\bfirstInstallTime=([0-9][0-9:\- TZ.+]{5,30})", RegexOptions.CultureInvariant);

    /// <summary>Liest die Ausgabe von «dumpsys package ch.hkplanung.kicad.quest». Ohne versionCode und versionName (oder bei «Unable to find package») ist die App nicht installiert.</summary>
    public static QuestAppInstalliert InstalliertLesen(string? ausgabe)
    {
        if (string.IsNullOrWhiteSpace(ausgabe) || ausgabe.Contains("Unable to find package", StringComparison.OrdinalIgnoreCase))
            return new QuestAppInstalliert(false, null, null, null, null);
        var name = VersionNameMuster.Match(ausgabe);
        var code = VersionCodeMuster.Match(ausgabe);
        if (!name.Success && !code.Success) return new QuestAppInstalliert(false, null, null, null, null);
        var akt = AktualisiertMuster.Match(ausgabe);
        var erst = ErstMuster.Match(ausgabe);
        return new QuestAppInstalliert(true, name.Success ? name.Groups[1].Value : null, code.Success ? int.Parse(code.Groups[1].Value) : null,
            akt.Success ? akt.Groups[1].Value.Trim() : null, erst.Success ? erst.Groups[1].Value.Trim() : null);
    }

    /// <summary>Vergleich der Fassungen für die Anzeige: nicht_installiert | andere_nummer | gleiche_nummer | unbekannt. Ob zwei Stände mit derselben Nummer gleich sind, zeigt nur der Commit.</summary>
    public static string FassungVergleich(QuestAppInstalliert? inst, QuestAppVersion? abgelegt)
    {
        if (inst is null || abgelegt is null) return "unbekannt";
        if (!inst.Installiert) return "nicht_installiert";
        if (abgelegt.VersionCode is { } a && inst.VersionCode is { } b) return a == b ? "gleiche_nummer" : "andere_nummer";
        if (abgelegt.VersionName is { } an && inst.VersionName is { } bn) return an == bn ? "gleiche_nummer" : "andere_nummer";
        return "unbekannt";
    }

    // ---------------------------------------------------------------- Zustand

    /// <summary>Zustand für den Dialog: Ablage am PC, Brille (wenn eine freigegeben ist) und deren installierte Fassung. Ändert nichts.</summary>
    public async Task<object> AppZustand()
    {
        var ablage = AppAblageLesen();
        await sperre.WaitAsync();
        try
        {
            var adb = AdbFinden(out var hinweis);
            if (adb is null)
                return new { paket = Marke.Paket, ablage, adbGefunden = false, adbHinweis = hinweis, brille = (object?)null, vergleich = "unbekannt" };
            var geraete = await GeraeteListe(adb);
            var g = geraete.Where(x => x.Zustand == "device").OrderBy(x => x.Wlan ? 0 : 1).FirstOrDefault();
            if (g is null)
                return new { paket = Marke.Paket, ablage, adbGefunden = true, brille = (object?)null, vergleich = "unbekannt" };
            var inst = await InstalliertVon(adb, g.Seriennummer);
            var modell = await ModellVon(adb, g.Seriennummer);
            return new
            {
                paket = Marke.Paket, ablage, adbGefunden = true,
                brille = new { seriennummer = g.Seriennummer, wlan = g.Wlan, modell, installiert = inst, kennung = await KennungVon(adb, g.Seriennummer) },
                appWeg = AppKabelAktiv(out _) ? "kabel" : AppWegLesen()?.Weg,   // «kabel»: keine Umstellung auf WLAN-adb, KICAD hält die Weiterleitung
                vergleich = FassungVergleich(inst, ablage.Version),
            };
        }
        finally { sperre.Release(); }
    }

    async Task<QuestAppInstalliert?> InstalliertVon(AdbFund adb, string serie)
    {
        var r = await AufrufGross(adb, "-s", serie, "shell", "dumpsys", "package", Marke.Paket);
        return r.Zeitueberschreitung || r.Code != 0 ? null : InstalliertLesen(r.Ausgabe);
    }

    async Task<string?> ModellVon(AdbFund adb, string serie)
    {
        var r = await Aufruf(adb, "-s", serie, "shell", "getprop", "ro.product.model");
        if (r.Zeitueberschreitung || r.Code != 0) return null;
        var t = new string(r.Ausgabe.Trim().Where(c => !char.IsControl(c)).ToArray());
        return t.Length is > 0 and <= 40 ? t : null;
    }

    // ---------------------------------------------------------------- Installieren

    /// <summary>Wählt den Weg für die Installation: bei «kabel» bevorzugt USB, bei «wlan» bevorzugt WLAN; sonst irgendein freigegebener.</summary>
    public static QuestGeraet? AppGeraetWaehlen(IEnumerable<QuestGeraet> geraete, string weg) =>
        geraete.Where(g => g.Zustand == "device").OrderBy(g => (weg == "wlan") == g.Wlan ? 0 : 1).FirstOrDefault();

    /// <summary>Die Meldung zu einem fehlgeschlagenen «adb install» (Meldungen von Android: INSTALL_FAILED_…). appName: Name der App in der Brille (Marke).</summary>
    public static (string Fehler, string Vorschlag) InstallFehler(string? ausgabe, string appName = "KICAD")
    {
        var a = ausgabe ?? "";
        if (a.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE", StringComparison.Ordinal) || a.Contains("INSTALL_PARSE_FAILED_INCONSISTENT_CERTIFICATES", StringComparison.Ordinal))
            return ("Die App in der Brille ist mit einem anderen Schlüssel signiert und lässt sich nicht überschreiben.",
                $"In der Brille die App «{appName}» deinstallieren (App-Bibliothek › {appName} › Deinstallieren) und erneut installieren.");
        if (a.Contains("INSTALL_FAILED_VERSION_DOWNGRADE", StringComparison.Ordinal))
            return ("In der Brille ist eine neuere Fassung der App installiert als die abgelegte.", "Die App in der Brille deinstallieren und erneut installieren, oder die neuere APK ablegen.");
        if (a.Contains("INSTALL_FAILED_INSUFFICIENT_STORAGE", StringComparison.Ordinal))
            return ("In der Brille ist nicht genug Speicher frei.", "In der Brille Platz schaffen (nicht benutzte Apps und Aufnahmen löschen) und erneut versuchen.");
        if (a.Contains("INSTALL_FAILED_OLDER_SDK", StringComparison.Ordinal) || a.Contains("INSTALL_FAILED_NO_MATCHING_ABIS", StringComparison.Ordinal))
            return ("Die App passt nicht zu dieser Brille.", "Eine Quest 3 mit aktueller Software verwenden.");
        return ("Die Installation ist fehlgeschlagen: " + Kuerzen(a.Trim().ReplaceLineEndings(" "), 300), "Brille aufsetzen und wach halten, Verbindung prüfen und erneut versuchen.");
    }

    /// <summary>Installiert oder aktualisiert die App und koppelt die Brille (Beschreibung oben in der Datei). weg: «wlan» oder «kabel».</summary>
    public async Task<QuestAppErgebnis> AppInstallieren(string? weg, string? name, XrNetzDienst netz, XrKopplungen kopplungen)
    {
        var w = (weg ?? "").Trim().ToLowerInvariant();
        if (w is not ("wlan" or "kabel")) throw new HlksFehler($"Der Weg «{Kuerzen(w, 20)}» ist unbekannt.", "Erlaubt: «wlan» (die Brille erreicht den PC über das Büro-WLAN) oder «kabel» (über das USB-Kabel, adb reverse).");
        if (!string.IsNullOrWhiteSpace(name)) XrKopplungen.NamePruefen(name, "Quest");   // früh: ein ungültiger Name ändert nichts (400)
        var schritte = new List<QuestAppSchritt>();
        QuestAppErgebnis Scheitert(string id, string text, string? vorschlag, string? brille = null, string? nameBrille = null)
        {
            schritte.Add(new QuestAppSchritt(id, text, false));
            return new QuestAppErgebnis(false, schritte, null, text, vorschlag, w, brille, nameBrille);
        }
        void Erledigt(string id, string text) => schritte.Add(new QuestAppSchritt(id, text, true));

        // 1. Voraussetzungen (ohne adb)
        if (!netz.Aktiv)
            return Scheitert("zugang", "Der Netzzugang dieses PCs ist aus.", $"Zuerst «Netzzugang einschalten» im Teil «Quest App» klicken. Die App verbindet sich nur über den Netzzugang (Port {netz.PortStandard}, nur gekoppelte Brillen).");
        var info = netz.Info();
        if (w == "wlan" && info.Ips.Count == 0)
            return Scheitert("zugang", $"{Marke.Name} findet keine Netzadresse dieses PCs im Büronetz.", "LAN oder WLAN verbinden (ohne VPN) und erneut versuchen – oder «über Kabel» installieren.");
        Erledigt("zugang", w == "kabel" ? $"Netzzugang aktiv (Port {info.Port}); die Brille erreicht ihn über das Kabel." : $"Netzzugang aktiv (Port {info.Port}), Adressen des PCs: {string.Join(", ", info.Ips)}.");
        var ablage = AppAblageLesen();
        if (ablage.Fehler is not null || ablage.ApkPfad is null || ablage.Version is null)
            return Scheitert("apk", ablage.Fehler ?? "Es liegt keine App in der Ablage.", $"{Marke.Bereitstellen} ausführen (baut die APK und legt sie ab).");
        Erledigt("apk", $"Abgelegte App: {Fassungstext(ablage.Version)}.");

        await sperre.WaitAsync();
        try
        {
            // 2. Brille
            AdbFund adb;
            QuestGeraet g;
            try
            {
                adb = AdbOderFehler();
                g = AppGeraetWaehlen(Freigegebene(await GeraeteListe(adb)), w)!;
            }
            catch (HlksFehler f) { return Scheitert("brille", f.Message, f.Vorschlag); }
            var modell = await ModellVon(adb, g.Seriennummer) ?? "Quest";
            var r0 = await Aufruf(adb, "-s", g.Seriennummer, "shell", "getprop", "ro.serialno");
            var kennung = g.Seriennummer;
            if (!r0.Zeitueberschreitung && r0.Code == 0)
            {
                try { kennung = XrKopplungen.BrillePruefen(r0.Ausgabe); }   // die Seriennummer der Brille ist auf beiden Wegen (USB, WLAN) dieselbe
                catch (HlksFehler) { /* unbrauchbar: die Kennung von adb gilt */ }
            }
            string brillenName;
            try { brillenName = XrKopplungen.NamePruefen(name, modell); }
            catch (HlksFehler f) { return Scheitert("brille", f.Message, f.Vorschlag); }
            Erledigt("brille", $"Brille: {modell} ({(g.Wlan ? "WLAN" : "USB")}).");

            // 3. App installieren (kann dauern: APK über adb)
            var inst = await AufrufLang(adb, einstellungen.InstallZeitlimit, "-s", g.Seriennummer, "install", "-r", ablage.ApkPfad);
            if (inst.Zeitueberschreitung)
                return Scheitert("installieren", $"adb hat bei «install» nicht innerhalb von {einstellungen.InstallZeitlimit.TotalSeconds:0} s geantwortet.", "Brille aufgesetzt und wach halten, Kabel bzw. WLAN prüfen und erneut versuchen.", kennung, brillenName);
            if (inst.Code != 0 || !Regex.IsMatch(inst.Ausgabe, @"\bSuccess\b"))
            {
                var (fehler, vorschlag) = InstallFehler(inst.Ausgabe, Marke.Name);
                return Scheitert("installieren", fehler, vorschlag, kennung, brillenName);
            }
            Erledigt("installieren", "App installiert (adb install -r).");

            // 4. Kabel: Weiterleitung des HTTPS-Ports
            var ips = AdressenFuerWeg(w, info.Ips);
            if (w == "kabel")
            {
                try { await WeiterleitungSetzen(adb, g.Seriennummer, info.Port); }
                catch (HlksFehler f) { return Scheitert("kabel", f.Message, f.Vorschlag, kennung, brillenName); }
                Erledigt("kabel", $"Kabel: adb reverse tcp:{info.Port}; das Paket nennt 127.0.0.1 als erste Adresse.");
            }

            // 5. Kopplung (Weg A) erzeugen und in die Brille legen
            XrPaket paket;
            try { paket = kopplungen.PaketErzeugen(kennung, brillenName, info with { Ips = ips }); }
            catch (HlksFehler f) { return Scheitert("kopplung", f.Message, f.Vorschlag, kennung, brillenName); }
            catch (XrFehler f) { return Scheitert("kopplung", f.Message, f.Vorschlag, kennung, brillenName); }
            try
            {
                var fehler = await PaketUebertragen(adb, g.Seriennummer, paket);
                if (fehler is not null)
                {
                    kopplungen.Widerrufen(paket.Kopplung);
                    return Scheitert("uebertragen", fehler, "Brille aufgesetzt und wach halten, Verbindung prüfen und erneut versuchen. Die Kopplung wurde zurückgenommen.", kennung, brillenName);
                }
            }
            catch (Exception ex) when (ex is HlksFehler or IOException or UnauthorizedAccessException)
            {
                kopplungen.Widerrufen(paket.Kopplung);
                return Scheitert("uebertragen", ex is HlksFehler h ? h.Message : "Das Paket liess sich am PC nicht für die Übertragung ablegen: " + ex.Message,
                    "Temporären Ordner und Rechte prüfen und erneut versuchen. Die Kopplung wurde zurückgenommen.", kennung, brillenName);
            }
            Erledigt("uebertragen", "Kopplungspaket in der Brille abgelegt (am PC gelöscht).");
            AppWegSchreiben(new AppWegStand(kennung, w, info.Port));   // ab jetzt gilt der App-Weg: keine Umstellung auf WLAN-adb im Kabelweg, Weiterleitung wird wiederhergestellt

            // 6. App starten (ein Fehler hier macht die Installation nicht ungültig)
            var start = await AppStarten(adb, g.Seriennummer);
            if (start is null) Erledigt("starten", "App gestartet.");
            else schritte.Add(new QuestAppSchritt("starten", start, false));
            var meldung = start is null
                ? $"{brillenName} ist installiert und gekoppelt, die App startet. Brille aufsetzen: sie verbindet sich von selbst."
                : $"{brillenName} ist installiert und gekoppelt, aber die App liess sich nicht starten ({start}). In der Brille die App «{Marke.Name}» in der App-Bibliothek öffnen.";
            return new QuestAppErgebnis(true, schritte, meldung, null, null, w, kennung, brillenName);
        }
        finally { sperre.Release(); }
    }

    /// <summary>Die Adressen im Paket: über WLAN die Adressen des PCs im Büronetz; über Kabel zuerst 127.0.0.1 (die Brille erreicht den PC dann über adb reverse), danach die übrigen.</summary>
    public static IReadOnlyList<string> AdressenFuerWeg(string weg, IReadOnlyList<string> ips) =>
        weg == "kabel" ? ["127.0.0.1", .. ips.Where(i => i != "127.0.0.1")] : ips;

    static string Fassungstext(QuestAppVersion v) =>
        string.Join(", ", new[] { v.VersionName is null ? null : $"Fassung {v.VersionName}" + (v.VersionCode is { } c ? $" ({c})" : ""), v.Commit is null ? null : $"Commit {v.Commit}", v.Zeit is null ? null : $"gebaut {v.Zeit}", $"{v.Groesse / 1024.0 / 1024.0:0.0} MB" }
            .Where(t => t is not null));

    /// <summary>Legt das Kopplungspaket mit «adb push» in den Eingang der App. Die Datei am PC liegt nur für die Dauer des Aufrufs im eigenen Ordner und wird danach überschrieben
    /// und gelöscht. Gibt den Fehlertext zurück (null = übertragen). Nie ein Teil des Pakets in einer Meldung.</summary>
    async Task<string?> PaketUebertragen(AdbFund adb, string serie, XrPaket paket)
    {
        var wurzel = einstellungen.PaketOrdner is { Length: > 0 } po ? po : Path.Combine(Path.GetTempPath(), "HLKS_CAD", "quest_app");
        var ordner = Path.Combine(wurzel, Guid.NewGuid().ToString("N")[..12]);
        var datei = Path.Combine(ordner, "kopplung.json");
        var mk = await Aufruf(adb, "-s", serie, "shell", "mkdir", "-p", Marke.Eingang);
        if (mk.Zeitueberschreitung) return "adb hat beim Anlegen des Eingangs der App nicht geantwortet.";
        if (mk.Code != 0) return "Der Eingang der App in der Brille liess sich nicht anlegen: " + Kuerzen(mk.Ausgabe, 200);
        Directory.CreateDirectory(ordner);
        try
        {
            File.WriteAllText(datei, Json.Text(paket), new UTF8Encoding(false));
            var r = await AufrufLang(adb, einstellungen.InstallZeitlimit, "-s", serie, "push", datei, Marke.Eingang + "/kopplung.json");
            if (r.Zeitueberschreitung) return $"adb hat bei «push» nicht innerhalb von {einstellungen.InstallZeitlimit.TotalSeconds:0} s geantwortet.";
            if (r.Code != 0) return "adb meldet bei «push»: " + Kuerzen(r.Ausgabe, 200);
            return null;
        }
        finally { PaketLoeschen(datei, ordner); }
    }

    /// <summary>Überschreibt die Paketdatei mit Nullen und löscht sie samt ihrem Ordner (der Schlüssel soll nirgends liegen bleiben).</summary>
    static void PaketLoeschen(string datei, string ordner)
    {
        try
        {
            if (File.Exists(datei))
            {
                var laenge = new FileInfo(datei).Length;
                File.WriteAllBytes(datei, new byte[laenge]);
                File.Delete(datei);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(datei); } catch (Exception ex2) when (ex2 is IOException or UnauthorizedAccessException) { /* gesperrt: der Ordner bleibt, die Datei ist überschrieben */ }
        }
        try { if (Directory.Exists(ordner)) Directory.Delete(ordner, true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* leer lassen */ }
    }

    /// <summary>Startet die App: Aktivität mit resolve-activity bestimmen, dann am start -n. Gibt den Fehlertext zurück (null = gestartet).</summary>
    async Task<string?> AppStarten(AdbFund adb, string serie)
    {
        var a = await Aufruf(adb, "-s", serie, "shell", "cmd", "package", "resolve-activity", "--brief", Marke.Paket);
        if (a.Zeitueberschreitung || a.Code != 0) return "die Startaktivität der App liess sich nicht bestimmen";
        var zeile = a.Ausgabe.Split('\n').Select(z => z.Trim()).LastOrDefault(z => z.Length > 0);
        if (zeile is null || !AktivitaetMuster.IsMatch(zeile)) return "die Startaktivität der App liess sich nicht bestimmen";
        var s = await Aufruf(adb, "-s", serie, "shell", "am", "start", "-n", zeile);
        if (s.Zeitueberschreitung) return "adb hat beim Start nicht geantwortet";
        if (s.Code != 0 || Regex.IsMatch(s.Ausgabe, @"\bError\b")) return "adb meldet beim Start: " + Kuerzen(s.Ausgabe.ReplaceLineEndings(" "), 150);
        return null;
    }
}
