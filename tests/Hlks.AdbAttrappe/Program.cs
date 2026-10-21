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
//   aufrufe.log    jeder Aufruf mit seinen Argumenten, eine Zeile (die Attrappe schreibt)
// Bekannte Aufrufe (optional mit «-s <Seriennummer>» davor): start-server, devices, reverse tcp:A tcp:B, reverse --list, reverse --remove tcp:A,
// shell ip -f inet addr show wlan0, tcpip 5555, connect <Adresse>. Alles andere meldet «unknown command» mit Code 1.
// Ein Weg mit «-s», der in geraete.txt nicht «device» ist, antwortet auf reverse/tcpip/shell wie adb: «error: device offline» bzw. «not found», Code 1.
using System.Text;

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
