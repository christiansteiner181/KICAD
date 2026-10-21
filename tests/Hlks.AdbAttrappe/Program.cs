// Attrappe statt adb.exe (Tests des Quest-Verbinders, Modul XR). Der Zustand liegt in Textdateien neben dem Programm (die Tests schreiben sie):
//   geraete.txt    Ausgabe von «adb devices» ab der zweiten Zeile, z. B. «1WMHH000000000<TAB>device»
//   reverse.txt    Weiterleitungen, eine je Zeile, z. B. «UsbFfs tcp:5130 tcp:5130»
//   ip.txt         Ausgabe von «adb shell ip -f inet addr show wlan0» (fehlt = Schnittstelle gibt es nicht, Code 1)
//   verhalten.txt  Schalter, je Zeile: haengt (devices schläft 60 s), connect_fehler, tcpip_fehler, reverse_fehler
//   aufrufe.log    jeder Aufruf mit seinen Argumenten, eine Zeile (die Attrappe schreibt)
// Bekannte Aufrufe (optional mit «-s <Seriennummer>» davor): start-server, devices, reverse tcp:A tcp:B, reverse --list, reverse --remove tcp:A,
// shell ip -f inet addr show wlan0, tcpip 5555, connect <Adresse>. Alles andere meldet «unknown command» mit Code 1.
using System.Text;

var dir = AppContext.BaseDirectory;
string P(string n) => Path.Combine(dir, n);
string[] Zeilen(string n) => File.Exists(P(n)) ? File.ReadAllLines(P(n)).Where(z => z.Length > 0).ToArray() : [];
var verhalten = Zeilen("verhalten.txt");
File.AppendAllText(P("aufrufe.log"), string.Join(' ', args) + Environment.NewLine, Encoding.UTF8);

var a = args.ToList();
if (a.Count >= 2 && a[0] == "-s") a.RemoveRange(0, 2);   // die Seriennummer wird mitprotokolliert (aufrufe.log), sonst nicht gebraucht

int Ende(string text, int code = 0) { if (text.Length > 0) Console.Out.WriteLine(text); return code; }

if (a.Count == 0) return Ende("Android Debug Bridge (Attrappe)");
switch (a[0])
{
    case "start-server":
        return Ende("* daemon started successfully");
    case "devices":
        if (verhalten.Contains("haengt")) Thread.Sleep(TimeSpan.FromSeconds(60));
        return Ende("List of devices attached\n" + string.Join('\n', Zeilen("geraete.txt")) + "\n");
    case "reverse" when a.Count == 2 && a[1] == "--list":
        return Ende(string.Join('\n', Zeilen("reverse.txt")));
    case "reverse" when a.Count == 3 && a[1] == "--remove" && a[2].StartsWith("tcp:"):
    {
        var alle = Zeilen("reverse.txt");
        var rest = alle.Where(z => z.Split(' ').ElementAtOrDefault(1) != a[2]).ToArray();
        if (rest.Length == alle.Length) return Ende($"adb: error: listener '{a[2]}' not found", 1);
        File.WriteAllLines(P("reverse.txt"), rest);
        return 0;
    }
    case "reverse" when a.Count == 3 && a[1].StartsWith("tcp:") && a[2].StartsWith("tcp:"):
        if (verhalten.Contains("reverse_fehler")) return Ende("adb: error: more than one device/emulator", 1);
        File.AppendAllText(P("reverse.txt"), $"UsbFfs {a[1]} {a[2]}{Environment.NewLine}");
        return Ende(a[1][4..]);
    case "shell" when string.Join(' ', a.Skip(1)) == "ip -f inet addr show wlan0":
        return File.Exists(P("ip.txt")) ? Ende(File.ReadAllText(P("ip.txt")).TrimEnd()) : Ende("Device \"wlan0\" does not exist.", 1);
    case "tcpip" when a.Count == 2 && a[1] == "5555":
        return verhalten.Contains("tcpip_fehler") ? Ende("error: closed", 1) : Ende("restarting in TCP mode port: 5555");
    case "connect" when a.Count == 2:
        if (verhalten.Contains("connect_fehler")) return Ende($"failed to connect to '{a[1]}': Connection refused");   // adb meldet das mit Code 0
        if (!Zeilen("geraete.txt").Any(z => z.StartsWith(a[1] + "\t"))) File.AppendAllText(P("geraete.txt"), $"{a[1]}\tdevice{Environment.NewLine}");
        return Ende($"connected to {a[1]}");
    default:
        return Ende($"adb: unknown command {a[0]}", 1);
}
