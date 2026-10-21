// Netz-Naht des Quest-Verbinders («Brille automatisch verbinden», Modul XR, Christian 21.10.2026: «könnte man nicht über MACadr. anstelle IP»):
// die Nachbartabelle des PCs (IP ↔ MAC), die aktiven IPv4-Schnittstellen und ein einzelner Ping. QuestDienst.cs entscheidet, wann und wie oft
// sie gebraucht werden (Anlass, Abstand von 2 Minuten, nur das eigene /24-Netz, keine VPN-/TAP-Adapter); hier steht nur, wie gelesen wird.
//
// SICHERHEIT:
// - Es wird kein Programm gestartet und keine Shell benutzt: die Nachbartabelle kommt über iphlpapi (GetIpNetTable2, nur lesend), die Schnittstellen
//   über System.Net.NetworkInformation, der Ping über System.Net.NetworkInformation.Ping (ICMP, kein TCP-Verbindungsaufbau, keine Daten ausser dem Standardpuffer).
// - Im Testlauf (FamilienBibliothekDienst.Testlauf()) ist die Naht leer (QuestNetzLeer): weder echte Tabelle noch echte Pings. Tests setzen eine eigene Fassung
//   über den Dienstcontainer (IQuestNetz).

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Hlks.Kern;

namespace Hlks.Web;

/// <summary>Ein Eintrag der Nachbartabelle: IPv4-Adresse und MAC (kleingeschrieben, «aa:bb:cc:dd:ee:ff»).</summary>
public sealed record NachbarEintrag(string Ip, string Mac);

/// <summary>Eine aktive IPv4-Adresse einer Netzwerkschnittstelle. Art ist der Name von NetworkInterfaceType (Ethernet, Wireless80211, Tunnel …).</summary>
public sealed record NetzSchnittstelle(string Name, string Beschreibung, string Art, string Ip, int Praefix);

/// <summary>Was der Quest-Verbinder vom Netz braucht. Die Vorgabe ist QuestNetzEcht; im Testlauf QuestNetzLeer.</summary>
public interface IQuestNetz
{
    /// <summary>Die Nachbartabelle (IPv4). Kann leer sein; wirft nicht.</summary>
    IReadOnlyList<NachbarEintrag> Nachbarn();
    /// <summary>Die aktiven IPv4-Schnittstellen (ungefiltert; die Auswahl trifft QuestDienst.PingNetze).</summary>
    IReadOnlyList<NetzSchnittstelle> Schnittstellen();
    /// <summary>Ein Ping an eine IPv4-Adresse mit Zeitgrenze; true, wenn eine Antwort kam. Wirft nicht.</summary>
    Task<bool> Anpingen(string ip, TimeSpan zeitgrenze, CancellationToken abbruch);

    /// <summary>Vorgabe für den Betrieb: im Testlauf leer, sonst echt.</summary>
    static IQuestNetz Vorgabe() => FamilienBibliothekDienst.Testlauf() ? new QuestNetzLeer() : new QuestNetzEcht();
}

/// <summary>Im Testlauf: keine Tabelle, keine Schnittstellen, kein Ping.</summary>
public sealed class QuestNetzLeer : IQuestNetz
{
    public IReadOnlyList<NachbarEintrag> Nachbarn() => [];
    public IReadOnlyList<NetzSchnittstelle> Schnittstellen() => [];
    public Task<bool> Anpingen(string ip, TimeSpan zeitgrenze, CancellationToken abbruch) => Task.FromResult(false);
}

/// <summary>Das echte Netz (Windows). Auf anderen Systemen bleibt die Tabelle leer.</summary>
public sealed class QuestNetzEcht : IQuestNetz
{
    public IReadOnlyList<NetzSchnittstelle> Schnittstellen()
    {
        var liste = new List<NetzSchnittstelle>();
        try
        {
            foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (n.OperationalStatus != OperationalStatus.Up || n.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var u in n.GetIPProperties().UnicastAddresses)
                    if (u.Address.AddressFamily == AddressFamily.InterNetwork)
                        liste.Add(new NetzSchnittstelle(n.Name, n.Description, n.NetworkInterfaceType.ToString(), u.Address.ToString(), u.PrefixLength));
            }
        }
        catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException) { /* keine Auskunft */ }
        return liste;
    }

    public async Task<bool> Anpingen(string ip, TimeSpan zeitgrenze, CancellationToken abbruch)
    {
        if (!IPAddress.TryParse(ip, out var adresse) || adresse.AddressFamily != AddressFamily.InterNetwork) return false;
        try
        {
            using var ping = new Ping();
            var antwort = await ping.SendPingAsync(adresse, (int)Math.Max(1, zeitgrenze.TotalMilliseconds)).WaitAsync(abbruch);
            return antwort.Status == IPStatus.Success;
        }
        catch (Exception ex) when (ex is PingException or SocketException or InvalidOperationException or OperationCanceledException) { return false; }
    }

    // ---------------------------------------------------------------- Nachbartabelle (iphlpapi, nur lesend)

    const int AF_INET = 2;
    // MIB_IPNET_ROW2: Address (SOCKADDR_INET, 28) | InterfaceIndex (4) | InterfaceLuid (8, ab 32) | PhysicalAddress[32] (ab 40) | PhysicalAddressLength (ab 72) | State (ab 76) | Flags (80) | ReachabilityTime (84); Grösse 88.
    // MIB_IPNET_TABLE2: NumEntries (4), dann ab Offset 8 die Zeilen (Ausrichtung auf 8).
    const int ZeilenStart = 8, ZeilenGroesse = 88, OffMac = 40, OffMacLaenge = 72, OffZustand = 76;

    [DllImport("iphlpapi.dll")] static extern int GetIpNetTable2(ushort familie, out IntPtr tabelle);
    [DllImport("iphlpapi.dll")] static extern void FreeMibTable(IntPtr speicher);

    public IReadOnlyList<NachbarEintrag> Nachbarn()
    {
        var liste = new List<NachbarEintrag>();
        if (!OperatingSystem.IsWindows()) return liste;
        IntPtr tabelle = IntPtr.Zero;
        try
        {
            if (GetIpNetTable2(AF_INET, out tabelle) != 0 || tabelle == IntPtr.Zero) return liste;
            int anzahl = Marshal.ReadInt32(tabelle, 0);
            if (anzahl is < 0 or > 100_000) return liste;
            for (int i = 0; i < anzahl; i++)
            {
                var z = IntPtr.Add(tabelle, ZeilenStart + i * ZeilenGroesse);
                if ((ushort)Marshal.ReadInt16(z, 0) != AF_INET) continue;
                int zustand = Marshal.ReadInt32(z, OffZustand);
                if (zustand is 0 or 1) continue;   // Unreachable, Incomplete: noch keine verlässliche MAC
                if (Marshal.ReadInt32(z, OffMacLaenge) != 6) continue;
                var ipBytes = new byte[4];
                Marshal.Copy(IntPtr.Add(z, 4), ipBytes, 0, 4);
                var mac = new byte[6];
                Marshal.Copy(IntPtr.Add(z, OffMac), mac, 0, 6);
                var text = string.Join(':', mac.Select(b => b.ToString("x2")));
                if (QuestDienst.MacPruefen(text) is { } ok) liste.Add(new NachbarEintrag(new IPAddress(ipBytes).ToString(), ok));
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or SEHException or ArgumentException) { liste.Clear(); }
        finally { if (tabelle != IntPtr.Zero) { try { FreeMibTable(tabelle); } catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { /* nichts zu tun */ } } }
        return liste;
    }
}
