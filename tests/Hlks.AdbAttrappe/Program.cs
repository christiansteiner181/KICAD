// Attrappe statt adb.exe (Tests des Quest-Verbinders, Modul XR). Der Zustand liegt in Textdateien neben dem Programm (die Tests schreiben sie):
//   geraete.txt    Ausgabe von «adb devices» ab der zweiten Zeile, z. B. «1WMHH000000000<TAB>device»
//   reverse.txt    Weiterleitungen, eine je Zeile, z. B. «UsbFfs tcp:5130 tcp:5130» (gilt für jedes Gerät; anders mit dem Schalter je_weg)
//   ip.txt         Ausgabe von «adb shell ip -f inet addr show wlan0» (fehlt = Schnittstelle gibt es nicht, Code 1)
//   mac.txt        WLAN-MAC der Quest für «shell ip link show wlan0» (fehlt = Schnittstelle gibt es nicht, Code 1)
//   verbindbar.txt Adressen (ohne Port), zu denen «connect» gelingt; ohne die Datei gelingt jede, mit ihr nur diese (sonst «failed to connect … timed out»)
//   verhalten.txt  Schalter, je Zeile: haengt (devices schläft 60 s), connect_fehler (connect scheitert immer), tcpip_fehler, reverse_fehler,
//                  je_weg (Weiterleitungen getrennt je Weg in reverse_<Seriennummer>.txt, «:» als «_»; «adb tcpip» löscht die des Weges, auf dem es läuft;
//                  ein Weg, der nicht in geraete.txt steht, ist «not found»)
//   connect_fehlschlaege.txt  Zahl N: die ersten N «connect» melden «failed to connect … Connection refused» (adbd startet in der Quest neu), danach geht es
//   wlan_offline.txt          Zahl M: ein durch «connect» neu hinzugekommener WLAN-Weg steht für die nächsten M «devices» als «offline», dann als «device»
//   reverse_fehlschlaege.txt  Zahl N: die ersten N «reverse tcp:A tcp:B» scheitern mit «error: device offline» (Code 1)
//   wach.txt       Autostart: Wachzustand für «shell dumpsys power» (mWakefulness=…), Vorgabe Awake
//   wach_ab.txt    Zahl N: die ersten N «dumpsys power» melden Asleep, danach gilt wach.txt
//   vorne.txt      Autostart: Paket der Vordergrund-App in «shell dumpsys activity activities», Vorgabe com.oculus.browser
//   am_start.log   (die Attrappe schreibt) die Adresse jedes «shell am start -a android.intent.action.VIEW -d <Adresse> com.oculus.browser»
//   forward.txt    (die Attrappe schreibt) die Weiterleitungen «forward tcp:A localabstract:chrome_devtools_remote»; «forward --remove tcp:A» entfernt sie
//   Quest App installieren (QuestApp.cs, Quest App P7):
//   model.txt      Modell für «shell getprop ro.product.model» (Vorgabe «Quest 3»); serialno.txt für «ro.serialno» (Vorgabe: Seriennummer des ersten USB-Wegs)
//   install_fehler.txt   Text nach «Failure [» für «install» (z. B. INSTALL_FAILED_UPDATE_INCOMPATIBLE); sonst gelingt es und merkt die App als installiert (app_installiert.txt)
//   install_version.txt  «<versionName> <versionCode>» der installierten App (Vorgabe «0.1 1»); app_installiert.txt gibt es, sobald «install» gelungen ist (der Test kann sie vorab anlegen)
//   verhalten.txt  zusätzlich: install_haengt, push_fehler, mkdir_fehler, start_fehler, aktivitaet_fehler
//   gepusht.txt    (die Attrappe schreibt) Inhalt der zuletzt mit «push» übertragenen Datei; push_ziel.txt das Ziel; push_quelle.txt der Pfad am PC und ob die Datei da war
//                  (H+K XR E1: App ch.hkplanung.xr; erlaubt ist nur das Ziel /sdcard/Android/data/ch.hkplanung.xr/files/kopplung_<werkzeug>.json, ein Paket je Werkzeug;
//                  gepusht_<werkzeug>.txt hält den Inhalt je Werkzeug)
//   Alte Apps entfernen (H+K XR E1, QuestApp.cs «AlteAppsEntfernen»):
//   alte_apps.txt  Pakete, die in der Brille installiert sind (eines je Zeile, z. B. ch.hkplanung.kicad.quest); «shell pm list packages <Filter>» listet sie als «package:<Paket>»,
//                  dazu ch.hkplanung.xr, sobald app_installiert.txt da ist; fehlt die Datei, sind keine alten Apps installiert
//   uninstall.log  (die Attrappe schreibt) jedes Paket, das «uninstall <Paket>» bekam (auch ein nicht installiertes); «uninstall» entfernt es aus alte_apps.txt (ch.hkplanung.xr:
//                  app_installiert.txt) und meldet «Success», sonst «Failure [DELETE_FAILED_INTERNAL_ERROR]» mit Code 1
//   verhalten.txt  zusätzlich: uninstall_fehler (uninstall scheitert immer), uninstall_haengt (schläft 60 s), pm_fehler (pm list packages scheitert)
//   app_start.log  (die Attrappe schreibt) jede Aktivität, die «am start -n» bekam
//   aufrufe.log    jeder Aufruf mit seinen Argumenten, eine Zeile (die Attrappe schreibt)
// Bekannte Aufrufe (optional mit «-s <Seriennummer>» davor): start-server, devices, reverse tcp:A tcp:B, reverse --list, reverse --remove tcp:A,
// shell ip -f inet addr show wlan0, tcpip 5555, connect <Adresse>, dazu die der Quest App (install -r, push, uninstall <Paket>, shell pm list packages <Filter>). Alles andere meldet «unknown command» mit Code 1.
// Ein Weg mit «-s», der in geraete.txt nicht «device» ist, antwortet auf reverse/tcpip/shell wie adb: «error: device offline» bzw. «not found», Code 1.
using System.Text;
using System.Text.RegularExpressions;

var dir = AppContext.BaseDirectory;
string P(string n) => Path.Combine(dir, n);
string[] Zeilen(string n) => File.Exists(P(n)) ? File.ReadAllLines(P(n)).Where(z => z.Length > 0).ToArray() : [];
var verhalten = Zeilen("verhalten.txt");
File.AppendAllText(P("aufrufe.log"), string.Join(' ', args) + Environment.NewLine, new UTF8Encoding(false));

var a = args.ToList();
string? serie = null;
if (a.Count >= 2 && a[0] == "-s") { serie = a[1]; a.RemoveRange(0, 2); }   // die Seriennummer wird mitprotokolliert (aufrufe.log)

int Ende(string text, int code = 0) { if (text.Length > 0) Console.Out.WriteLine(text); return code; }

// Zähler in einer Datei: liefert true, solange noch Fehlversuche offen sind, und zählt herunter
bool Verbraucht(string datei)
{
    var z = Zeilen(datei);
    if (z.Length == 0 || !int.TryParse(z[0], out var n) || n <= 0) return false;
    File.WriteAllText(P(datei), (n - 1).ToString());
    return true;
}
int Zaehler(string datei) => Zeilen(datei) is { Length: > 0 } z && int.TryParse(z[0], out var n) ? n : 0;

bool JeWeg = verhalten.Contains("je_weg");
string ReverseDatei() => serie is not null && JeWeg ? "reverse_" + serie.Replace(':', '_') + ".txt" : "reverse.txt";

// Zustand des Weges, auf dem der Aufruf läuft (null = nicht in geraete.txt)
string? Zustand(string s) => Zeilen("geraete.txt").Select(z => z.Split('\t')).Where(t => t.Length == 2 && t[0] == s).Select(t => t[1]).FirstOrDefault();
int? WegFehler()
{
    if (serie is null) return null;
    var z = Zustand(serie);
    if (z is null) { if (JeWeg) { Console.Out.WriteLine($"adb: device '{serie}' not found"); return 1; } return null; }
    if (z == "device") return null;
    Console.Out.WriteLine(z == "offline" ? "error: device offline" : $"error: device {z}");
    return 1;
}

if (a.Count == 0) return Ende("Android Debug Bridge (Attrappe)");
switch (a[0])
{
    case "start-server":
        return Ende("* daemon started successfully");
    case "devices":
    {
        if (verhalten.Contains("haengt")) Thread.Sleep(TimeSpan.FromSeconds(60));
        var zeilen = Zeilen("geraete.txt");
        // ein neu verbundener WLAN-Weg ist nach dem Neustart von adbd kurz «offline»
        if (zeilen.Any(z => z.Contains(':') && z.EndsWith("\toffline")) && !Verbraucht("wlan_offline.txt"))
        {
            zeilen = zeilen.Select(z => z.Contains(':') && z.EndsWith("\toffline") ? z[..^"offline".Length] + "device" : z).ToArray();
            File.WriteAllLines(P("geraete.txt"), zeilen);
        }
        return Ende("List of devices attached\n" + string.Join('\n', zeilen) + "\n");
    }
    case "reverse" when a.Count == 2 && a[1] == "--list":
        if (WegFehler() is { } f1) return f1;
        return Ende(string.Join('\n', Zeilen(ReverseDatei())));
    case "reverse" when a.Count == 3 && a[1] == "--remove" && a[2].StartsWith("tcp:"):
    {
        if (WegFehler() is { } f2) return f2;
        var alle = Zeilen(ReverseDatei());
        var rest = alle.Where(z => z.Split(' ').ElementAtOrDefault(1) != a[2]).ToArray();
        if (rest.Length == alle.Length) return Ende($"adb: error: listener '{a[2]}' not found", 1);
        File.WriteAllLines(P(ReverseDatei()), rest);
        return 0;
    }
    case "reverse" when a.Count == 3 && a[1].StartsWith("tcp:") && a[2].StartsWith("tcp:"):
        if (verhalten.Contains("reverse_fehler")) return Ende("adb: error: more than one device/emulator", 1);
        if (WegFehler() is { } f3) return f3;
        if (Verbraucht("reverse_fehlschlaege.txt")) return Ende("error: device offline", 1);
        File.AppendAllText(P(ReverseDatei()), $"UsbFfs {a[1]} {a[2]}{Environment.NewLine}");
        return Ende(a[1][4..]);
    case "shell" when string.Join(' ', a.Skip(1)) == "ip -f inet addr show wlan0":
        if (WegFehler() is { } f4) return f4;
        return File.Exists(P("ip.txt")) ? Ende(File.ReadAllText(P("ip.txt")).TrimEnd()) : Ende("Device \"wlan0\" does not exist.", 1);
    case "shell" when string.Join(' ', a.Skip(1)) == "ip link show wlan0":
        if (WegFehler() is { } f5) return f5;
        return File.Exists(P("mac.txt"))
            ? Ende("21: wlan0: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 qdisc mq state UP mode DEFAULT group default qlen 1000\n    link/ether " + File.ReadAllText(P("mac.txt")).Trim() + " brd ff:ff:ff:ff:ff:ff")
            : Ende("Device \"wlan0\" does not exist.", 1);
    // Autostart (QuestStarter.cs): Browser öffnen, Wachzustand, Vordergrund-App, Fern-Debugging-Weiterleitung
    case "shell" when a.Count == 8 && string.Join(' ', a.Skip(1).Take(5)) == "am start -a android.intent.action.VIEW -d"
                      && System.Text.RegularExpressions.Regex.IsMatch(a[6], @"^http://localhost:\d{1,5}/vr$") && a[7] == "com.oculus.browser":
        if (WegFehler() is { } f6) return f6;
        File.AppendAllText(P("am_start.log"), a[6] + Environment.NewLine);
        return Ende("Starting: Intent { act=android.intent.action.VIEW dat=" + a[6] + " pkg=com.oculus.browser }");
    case "shell" when string.Join(' ', a.Skip(1)) == "dumpsys power":
    {
        if (WegFehler() is { } f7) return f7;
        var w = Verbraucht("wach_ab.txt") ? "Asleep" : (Zeilen("wach.txt") is { Length: > 0 } wz ? wz[0] : "Awake");
        return Ende("POWER MANAGER (dumpsys power)\n\nPower Manager State:\n  mWakefulness=" + w + "\n  mWakefulnessChanging=false\n  mHalAutoSuspendModeEnabled=false");
    }
    case "shell" when string.Join(' ', a.Skip(1)) == "dumpsys activity activities":
    {
        if (WegFehler() is { } f8) return f8;
        var pkg = Zeilen("vorne.txt") is { Length: > 0 } vz ? vz[0] : "com.oculus.browser";
        return Ende("ACTIVITY MANAGER ACTIVITIES (dumpsys activity activities)\n  mFocusedApp=ActivityRecord{9f8e7d u0 " + pkg + "/.Main t7}\n"
            + "  mResumedActivity: ActivityRecord{1a2b3c u0 " + pkg + "/.Main t7}\n  topResumedActivity=ActivityRecord{1a2b3c u0 " + pkg + "/.Main t7}");
    }
    case "forward" when a.Count == 3 && a[1].StartsWith("tcp:") && a[2] == "localabstract:chrome_devtools_remote":
        if (WegFehler() is { } f9) return f9;
        File.AppendAllText(P("forward.txt"), a[1] + " " + a[2] + Environment.NewLine);
        return Ende(a[1][4..]);
    case "forward" when a.Count == 3 && a[1] == "--remove" && a[2].StartsWith("tcp:"):
    {
        if (WegFehler() is { } f10) return f10;
        var alleF = Zeilen("forward.txt");
        var restF = alleF.Where(z => z.Split(' ')[0] != a[2]).ToArray();
        if (restF.Length == alleF.Length) return Ende($"adb: error: listener '{a[2]}' not found", 1);
        File.WriteAllLines(P("forward.txt"), restF);
        return 0;
    }
    // Quest App installieren (QuestApp.cs)
    case "shell" when string.Join(' ', a.Skip(1)) == "getprop ro.product.model":
        if (WegFehler() is { } g1) return g1;
        return Ende(File.Exists(P("model.txt")) ? File.ReadAllText(P("model.txt")).Trim() : "Quest 3");
    case "shell" when string.Join(' ', a.Skip(1)) == "getprop ro.serialno":
        if (WegFehler() is { } g2) return g2;
        return Ende(File.Exists(P("serialno.txt")) ? File.ReadAllText(P("serialno.txt")).Trim()
            : Zeilen("geraete.txt").Select(z => z.Split('\t')[0]).FirstOrDefault(z => !z.Contains(':')) ?? "");
    case "install" when a.Count == 3 && a[1] == "-r":
    {
        if (WegFehler() is { } g3) return g3;
        if (verhalten.Contains("install_haengt")) Thread.Sleep(TimeSpan.FromSeconds(60));
        if (!File.Exists(a[2])) return Ende($"adb: failed to stat {a[2]}: No such file or directory", 1);
        if (File.Exists(P("install_fehler.txt")))
            return Ende("Performing Streamed Install\nadb: failed to install " + a[2] + ": Failure [" + File.ReadAllText(P("install_fehler.txt")).Trim() + "]", 1);
        File.AppendAllText(P("installiert.log"), a[2] + Environment.NewLine);
        File.WriteAllText(P("app_installiert.txt"), File.Exists(P("install_version.txt")) ? File.ReadAllText(P("install_version.txt")).Trim() : "0.1 1");
        return Ende("Performing Streamed Install\nSuccess");
    }
    case "shell" when string.Join(' ', a.Skip(1)).StartsWith("mkdir -p /sdcard/Android/data/ch.hkplanung.xr/files"):
        if (WegFehler() is { } g4) return g4;
        return verhalten.Contains("mkdir_fehler") ? Ende("mkdir: '/sdcard/Android/data/ch.hkplanung.xr/files': Permission denied", 1) : 0;
    // H+K XR E1: ein Paket je Werkzeug, Ziel kopplung_<werkzeug>.json im Eingang der App (das alte kopplung.json gibt es nicht mehr: «unknown command»)
    case "push" when a.Count == 3 && Regex.IsMatch(a[2], @"^/sdcard/Android/data/ch\.hkplanung\.xr/files/kopplung_[a-z][a-z0-9_-]{0,31}\.json$"):
    {
        if (WegFehler() is { } g5) return g5;
        if (verhalten.Contains("push_fehler")) return Ende("adb: error: failed to copy '" + a[1] + "' to '" + a[2] + "': remote couldn't create file: Permission denied", 1);
        File.AppendAllText(P("push_quelle.txt"), a[1] + " da=" + File.Exists(a[1]) + Environment.NewLine);
        if (!File.Exists(a[1])) return Ende("adb: error: cannot stat '" + a[1] + "': No such file or directory", 1);
        File.WriteAllText(P("gepusht.txt"), File.ReadAllText(a[1]));
        File.WriteAllText(P("gepusht_" + Regex.Match(a[2], @"kopplung_(.+)\.json$").Groups[1].Value + ".txt"), File.ReadAllText(a[1]));   // je Werkzeug
        File.AppendAllText(P("push_ziel.txt"), a[2] + Environment.NewLine);
        return Ende(a[1] + ": 1 file pushed, 0 skipped. 0.1 MB/s (600 bytes in 0.005s)");
    }
    case "shell" when string.Join(' ', a.Skip(1)) == "dumpsys package ch.hkplanung.xr":
    {
        if (WegFehler() is { } g6) return g6;
        if (!File.Exists(P("app_installiert.txt"))) return Ende("Unable to find package: ch.hkplanung.xr");
        var v = File.ReadAllText(P("app_installiert.txt")).Trim().Split(' ');
        return Ende("Packages:\n  Package [ch.hkplanung.xr] (a1b2c3):\n    userId=10234\n    versionCode=" + v.ElementAtOrDefault(1) + " minSdk=29 targetSdk=32\n    versionName=" + v[0]
            + "\n    firstInstallTime=2026-10-23 17:59:10\n    lastUpdateTime=2026-10-23 21:15:42\n    installerPackageName=null");
    }
    case "shell" when string.Join(' ', a.Skip(1)) == "cmd package resolve-activity --brief ch.hkplanung.xr":
        if (WegFehler() is { } g7) return g7;
        if (verhalten.Contains("aktivitaet_fehler")) return Ende("No activity found", 1);
        return Ende("priority=0 preferredOrder=0 match=0x108000 specificIndex=-1 isDefault=true\nch.hkplanung.xr/crc6412ab34cd.MainActivity");
    case "shell" when a.Count == 5 && a[1] == "am" && a[2] == "start" && a[3] == "-n" && a[4].StartsWith("ch.hkplanung.xr/"):
        if (WegFehler() is { } g8) return g8;
        if (verhalten.Contains("start_fehler")) return Ende("Starting: Intent { cmp=" + a[4] + " }\nError type 3\nError: Activity class {" + a[4] + "} does not exist.");
        File.AppendAllText(P("app_start.log"), a[4] + Environment.NewLine);
        return Ende("Starting: Intent { cmp=" + a[4] + " }");
    // Alte Apps entfernen (QuestApp.cs, H+K XR E1): installierte Pakete lesen und deinstallieren
    case "shell" when a.Count == 5 && a[1] == "pm" && a[2] == "list" && a[3] == "packages":
    {
        if (WegFehler() is { } h1) return h1;
        if (verhalten.Contains("pm_fehler")) return Ende("Error: Could not access the Package Manager.  Is the system running?", 1);
        var pakete = Zeilen("alte_apps.txt").ToList();
        if (File.Exists(P("app_installiert.txt"))) pakete.Add("ch.hkplanung.xr");
        pakete.Add("com.oculus.browser");   // fremde Pakete stehen in der echten Liste auch; der Filter (a[4]) lässt sie weg
        return Ende(string.Join('\n', pakete.Where(p => p.Contains(a[4], StringComparison.Ordinal)).Select(p => "package:" + p)));
    }
    case "uninstall" when a.Count == 2:
    {
        if (WegFehler() is { } h2) return h2;
        File.AppendAllText(P("uninstall.log"), a[1] + Environment.NewLine);
        if (verhalten.Contains("uninstall_haengt")) Thread.Sleep(TimeSpan.FromSeconds(60));
        if (verhalten.Contains("uninstall_fehler")) return Ende("Failure [DELETE_FAILED_DEVICE_POLICY_MANAGER]", 1);
        var alle = Zeilen("alte_apps.txt");
        if (alle.Contains(a[1])) { File.WriteAllLines(P("alte_apps.txt"), alle.Where(z => z != a[1]).ToArray()); return Ende("Success"); }
        if (a[1] == "ch.hkplanung.xr" && File.Exists(P("app_installiert.txt"))) { File.Delete(P("app_installiert.txt")); return Ende("Success"); }
        return Ende("Failure [DELETE_FAILED_INTERNAL_ERROR]", 1);
    }
    case "tcpip" when a.Count == 2 && a[1] == "5555":
        if (verhalten.Contains("tcpip_fehler")) return Ende("error: closed", 1);
        if (JeWeg && serie is not null && File.Exists(P(ReverseDatei()))) File.Delete(P(ReverseDatei()));   // adbd startet neu: Weiterleitungen dieses Weges sind weg
        return Ende("restarting in TCP mode port: 5555");
    case "connect" when a.Count == 2:
    {
        if (verhalten.Contains("connect_fehler")) return Ende($"failed to connect to '{a[1]}': Connection refused");   // adb meldet das mit Code 0
        // verbindbar.txt: nur diese Adressen (ohne Port) antworten; alle anderen laufen in die Zeitüberschreitung (adb meldet auch das mit Code 0)
        if (Zeilen("verbindbar.txt") is { Length: > 0 } erlaubt && !erlaubt.Contains(a[1].Split(':')[0]))
            return Ende($"failed to connect to '{a[1]}': Connection timed out");
        if (Verbraucht("connect_fehlschlaege.txt")) return Ende($"failed to connect to '{a[1]}': Connection refused");
        if (Zeilen("geraete.txt").Any(z => z.StartsWith(a[1] + "\t"))) return Ende($"already connected to {a[1]}");
        File.AppendAllText(P("geraete.txt"), $"{a[1]}\t{(Zaehler("wlan_offline.txt") > 0 ? "offline" : "device")}{Environment.NewLine}");
        return Ende($"connected to {a[1]}");
    }
    default:
        return Ende($"adb: unknown command {a[0]}", 1);
}
