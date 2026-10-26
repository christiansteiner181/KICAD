// Gesten am Arbeitsplatz (Modul G): die Steuerung zwischen Oberfläche und der Gestenerkennung (gesten.js, hlks-kern).
// Sie lädt gesten.js nur per dynamischem import(), wenn der Grundschalter an ist bzw. der Dialog es braucht – ohne eingeschaltete Gesten gibt es keine Last und
// keine Kamera. Fehlt das Modul oder wirft starten(), meldet sie das sauber (Zustand «aus» mit Hinweis) und bricht den Start von KICAD nie ab.
// Sie hält die Einstellung (je Benutzer über OneDrive, Server: GestenEinstellung.cs), den Zustand für Statusleiste und Menüband und verteilt die Ereignisse
// von gesten.js (zustand, punkte, schritt) an die Oberfläche. Das Kamerabild sieht sie nie: gesten.js liefert nur Zustände, Punkte und Schritt-Gesten.
//
// steuerungErzeugen(abh) ist rein (ohne DOM) und in Node mit einer Attrappe von gesten.js getestet; die echte Verdrahtung steht unten (steuerung).

import { benutzerLaden, benutzerWert, benutzerSetzen } from '../oberflaeche/benutzer.js';
import { SCHLUESSEL, einstellungNormal, fehlerText, NICHT_EINGEBAUT } from './gesten-text.js';

/**
 * abh = {
 *   laden()        → Promise<Modul>   lädt gesten.js (import('./gesten.js'))
 *   stellschrauben() → Promise<Objekt|null>   STELLSCHRAUBEN_VORGABE aus gesten-logik.js (für den Dialog)
 *   lesen()        → Promise<Wert|null>   die gespeicherte Einstellung
 *   schreiben(w)   → Promise            speichert die Einstellung
 *   melden(text, art, opt)              Meldung an den Benutzer (art: info | warnung | fehler)
 *   erlaubt()      → bool               false im eigenen Fenster einer Ansicht (dort laufen keine Gesten)
 * }
 * Ereignisse der Steuerung (abonnieren): die von gesten.js ({ art: 'zustand' | 'punkte' | 'schritt', … }) und dazu
 *   { art: 'schalter' }       Grundschalter, Start, Stopp oder Fehler haben sich geändert
 *   { art: 'einstellung' }    die Einstellung hat sich geändert
 *   { art: 'testmodus', an }  das Testfeld wurde geöffnet bzw. geschlossen
 */
export function steuerungErzeugen(abh = {}) {
  const { laden, stellschrauben: vorgabeLaden = async () => null, lesen = async () => null, schreiben = async () => {}, melden = () => {}, erlaubt = () => true } = abh;
  let einst = einstellungNormal(null);
  let modul = null;
  let abmelden = null;
  let laeuft = false;
  let startet = false;
  let status = { zustand: 'aus', geste: null };
  let fehler = null;
  let test = false;
  let aufnahme = false;
  const hoerer = new Set();

  const ruf = (e) => { for (const fn of [...hoerer]) { try { fn(e); } catch (x) { console.error('Gesten', x); } } };

  /** Lädt gesten.js (einmal). Ein fehlendes Modul ergibt eine deutsche Meldung, keine Ausnahme nach aussen. */
  async function modulHolen() {
    if (modul) return modul;
    let m;
    try { m = await laden?.(); } catch (e) { console.error('gesten.js', e); throw new Error(NICHT_EINGEBAUT); }
    if (!m || typeof m.starten !== 'function') throw new Error(NICHT_EINGEBAUT);
    modul = m;
    return modul;
  }

  async function speichern() {
    try { await schreiben(einst); }
    catch (e) { melden(`Die Einstellung der Gesten konnte nicht gespeichert werden: ${e?.message ?? e}`, 'warnung', { vorschlag: e?.vorschlag }); }
  }

  function innen(e) {
    if (!e || typeof e !== 'object') return;
    if (e.art === 'zustand') {
      status = { zustand: e.zustand ?? 'aus', geste: e.geste ?? null };
      if (e.fehler) {
        fehler = fehlerText(e.fehler);
        if (status.zustand === 'aus') { melden(fehler, 'warnung'); abgebrochen(); return; }
      }
    }
    ruf(e);
  }

  /** gesten.js hat sich selbst beendet (z. B. Kamera verloren): Zustand «aus», der Grundschalter geht mit aus, damit der nächste Start nicht erneut scheitert. */
  function abgebrochen() {
    try { abmelden?.(); } catch { /* egal */ }
    abmelden = null;
    laeuft = false;
    startet = false;
    status = { zustand: 'aus', geste: null };
    einst = { ...einst, an: false };
    speichern();
    ruf({ art: 'punkte', punkte: null });
    ruf({ art: 'schalter' });
  }

  async function starten() {
    if (laeuft || startet) return laeuft;
    startet = true;
    fehler = null;
    ruf({ art: 'schalter' });
    try {
      const m = await modulHolen();
      abmelden = m.abonnieren?.(innen) ?? null;
      await m.starten({ hand: einst.hand, kameraId: einst.kameraId, stellschrauben: { ...einst.stellschrauben } });
    } catch (e) {
      try { abmelden?.(); } catch { /* egal */ }
      abmelden = null;
      startet = false;
      laeuft = false;
      status = { zustand: 'aus', geste: null };
      fehler = fehlerText(e);
      einst = { ...einst, an: false };
      melden(fehler, 'warnung');
      ruf({ art: 'punkte', punkte: null });
      ruf({ art: 'schalter' });
      return false;
    }
    startet = false;
    laeuft = true;
    if (status.zustand === 'aus') status = { zustand: 'bereit', geste: null };
    if (test) { try { modul.testmodus?.(true); } catch { /* egal */ } }
    ruf({ art: 'schalter' });
    return true;
  }

  async function stoppen() {
    const m = modul;
    try { abmelden?.(); } catch { /* egal */ }
    abmelden = null;
    if (aufnahme) { aufnahme = false; try { await m?.aufnahmeBeenden?.(); } catch { /* egal */ } }
    try { await m?.stoppen?.(); } catch (e) { console.error('Gesten stoppen', e); }
    laeuft = false;
    startet = false;
    status = { zustand: 'aus', geste: null };
    ruf({ art: 'punkte', punkte: null });
    ruf({ art: 'schalter' });
  }

  const api = {
    /** Liest die Einstellung und startet die Gesten, wenn der Grundschalter an ist. Wirft nie. */
    async einrichten() {
      if (!erlaubt()) return false;
      try { einst = einstellungNormal(await lesen()); } catch { einst = einstellungNormal(null); }
      ruf({ art: 'einstellung' });
      ruf({ art: 'schalter' });
      if (einst.an) return starten();
      return false;
    },

    /** Der Grundschalter (Aktion ansicht.gesten, Klick auf das Symbol der Statusleiste). Ohne Angabe wird umgeschaltet. Ergebnis: ob die Gesten jetzt an sind. */
    async schalten(an = !(einst.an && (laeuft || startet))) {
      if (!erlaubt()) return false;
      an = !!an;
      if (an) {
        einst = { ...einst, an: true };
        const ok = await starten();
        if (ok) await speichern();
        return ok;
      }
      einst = { ...einst, an: false };
      await stoppen();
      await speichern();
      return false;
    },

    /** Der Grundschalter steht auf «an» (gewünscht), auch wenn die Kamera gerade startet. */
    an: () => einst.an && (laeuft || startet),
    laeuft: () => laeuft,
    einstellung: () => ({ ...einst, stellschrauben: { ...einst.stellschrauben }, belegung: { G2: { ...einst.belegung.G2 }, G6: einst.belegung.G6 } }),

    /** Zustand für Statusleiste und Dialog (siehe gesten-text.js › statusAnzeige). */
    zustand: () => ({ an: einst.an && (laeuft || startet), laeuft, startet, zustand: laeuft ? status.zustand : 'aus', geste: laeuft ? status.geste : null, fehler, test, aufnahme }),

    /** Ändert Teile der Einstellung (hand, kameraId, skelett, stellschrauben, belegung), speichert sie und gibt hand, kameraId und stellschrauben an gesten.js weiter. */
    async einstellungSetzen(teil = {}) {
      const alt = einst;
      einst = einstellungNormal({ ...einst, ...teil });
      einst.an = alt.an;
      ruf({ art: 'einstellung' });
      await speichern();
      if (laeuft && modul?.einstellen) {
        const weg = {};
        if (einst.hand !== alt.hand) weg.hand = einst.hand;
        if (einst.kameraId !== alt.kameraId) weg.kameraId = einst.kameraId;
        if (JSON.stringify(einst.stellschrauben) !== JSON.stringify(alt.stellschrauben)) weg.stellschrauben = { ...einst.stellschrauben };
        if (Object.keys(weg).length) { try { await modul.einstellen(weg); } catch (e) { melden(fehlerText(e), 'warnung'); } }
      }
      return api.einstellung();
    },

    /** Testfeld: erkannte Gesten laufen weiter an die Oberfläche, lösen aber nichts aus (weder gesten.js noch gesten-ausloesen.js). */
    testmodus(an) {
      test = !!an;
      if (laeuft) { try { modul?.testmodus?.(test); } catch (e) { console.error('Gesten Testmodus', e); } }
      ruf({ art: 'testmodus', an: test });
    },
    istTest: () => test,

    /** Kameras zur Auswahl [{ id, name }]; lädt gesten.js, ohne die Kamera zu öffnen. Ohne Modul: leere Liste. */
    async kameras() {
      try { const m = await modulHolen(); return (await m.kameras?.()) ?? []; } catch { return []; }
    },
    /** Das Modul ist da (der Dialog braucht seine Stellschrauben). */
    async stellschraubenVorgabe() {
      try { return (await vorgabeLaden()) ?? null; } catch { return null; }
    },

    /** Punktfolge aufnehmen (nur Punkte, nie Bilder). Wirft, wenn die Gesten aus sind. */
    aufnahmeStarten() {
      if (!laeuft || !modul?.aufnahmeStarten) throw new Error('Zum Aufnehmen müssen die Gesten eingeschaltet sein.');
      modul.aufnahmeStarten();
      aufnahme = true;
      ruf({ art: 'schalter' });
    },
    async aufnahmeBeenden() {
      if (!aufnahme) return null;
      aufnahme = false;
      ruf({ art: 'schalter' });
      return (await modul?.aufnahmeBeenden?.()) ?? null;
    },

    abonnieren(fn) { hoerer.add(fn); return () => hoerer.delete(fn); },
  };
  return api;
}

// ---------------------------------------------------------------------------------------------- die echte Steuerung

/** Meldung der Oberfläche; dialoge.js wird erst beim ersten Gebrauch geladen (es greift beim Laden auf das Dokument zu). */
async function meldenEcht(text, art, opt) {
  try { (await import('../oberflaeche/dialoge.js')).meldung(text, art, opt); } catch (e) { console.warn(text, e); }
}

/** Die Steuerung der Oberfläche: gesten.js wird nur auf Wunsch geladen (dynamischer import). */
export const steuerung = steuerungErzeugen({
  laden: () => import('./gesten.js'),
  stellschrauben: async () => (await import('./gesten-logik.js')).STELLSCHRAUBEN_VORGABE,
  lesen: async () => { await benutzerLaden(); return benutzerWert(SCHLUESSEL, null); },
  schreiben: (w) => benutzerSetzen(SCHLUESSEL, w),
  melden: (t, a, o) => { meldenEcht(t, a, o); },
  erlaubt: () => typeof location === 'undefined' || !(Number(new URLSearchParams(location.search).get('fenster')) >= 2),   // nicht im eigenen Fenster einer Ansicht (zweitfenster.js)
});
