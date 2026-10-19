// Stufenwahl des Menübands (Teil R, entwurf/menueband-revit.md): reine Funktionen ohne DOM, in Node testbar.
//
// Stufen je Gruppe (wie Revit/Office, von gross nach klein):
//   G  gross:        32-px-Symbol mit Text darunter (Aktionen mit groesse «klein» stehen als M-Spalten daneben)
//   M  mittel:       16-px-Symbol mit Text daneben, drei Knöpfe übereinander
//   K  klein:        nur 16-px-Symbole, drei übereinander (Text im Tooltip)
//   Z  zusammengeklappt: ein Knopf mit Symbol und Namen der Gruppe, die Gruppe öffnet als Flyout (in Stufe G)
// Aktionen mit `immerGross` bleiben in M und K gross (32 px mit Text), bis die Gruppe zu Z klappt.
//
// Reihenfolge: Wird der Platz knapp, wird zuerst die rechteste Gruppe eine Stufe kleiner, dann die nächste nach links usw.
// Ein Durchgang bringt jede Gruppe von rechts nach links höchstens auf die Stufe des Durchgangs (1: M, 2: K, 3: Z); die erste Gruppe
// eines Registers wird also zuletzt verkleinert, und keine Gruppe springt in einem Durchgang über eine Stufe. Stufen, die nicht
// schmaler machen (z. B. M bei nur einem Knopf), werden übersprungen; die Gruppe wartet dann auf den nächsten Durchgang.
// Reicht auch «alles auf der letzten Stufe» nicht, wandern die Gruppen von rechts nach «» weitere» (Flyout) – nie ein Rollbalken.

export const STUFEN = ['G', 'M', 'K', 'Z'];
/** Spalten mit kleinen Knöpfen haben höchstens so viele Knöpfe übereinander (Gruppenhöhe 70 px). */
export const PRO_SPALTE = 3;

const TOLERANZ = 1;   // Breite in px, um die eine Stufe mindestens schmaler sein muss, um verwendet zu werden

/**
 * Teilt die Aktionen einer Gruppe in Spalten auf.
 *   liste  [{ groesse: 'gross'|'klein', immerGross?: bool, breit?: bool }]
 * Ergebnis: [{ art: 'gross', item } | { art: 'spalte', stil: 'mittel'|'klein', items: [...] }]
 * G: wie bisher – Aktionen mit groesse «klein» zu je drei in M-Spalten, alles andere gross.
 * M/K: alles ausser immerGross in Spalten zu je drei (mittel bzw. klein), immerGross bleibt gross.
 * Z hat keinen Plan (Flyout zeigt die Gruppe in Stufe G).
 */
export function gruppenPlan(liste, stufe) {
  const plan = [];
  const klein = (a) => (stufe === 'G' ? a.groesse === 'klein' && !a.immerGross : !a.immerGross);
  const stil = stufe === 'K' ? 'klein' : 'mittel';
  for (let i = 0; i < liste.length;) {
    if (klein(liste[i])) {
      const items = [];
      while (items.length < PRO_SPALTE && i < liste.length && klein(liste[i])) items.push(liste[i++]);
      plan.push({ art: 'spalte', stil, items });
    } else plan.push({ art: 'gross', item: liste[i++] });
  }
  return plan;
}

/** Stufen einer Gruppe, die tatsächlich schmaler werden: G immer, danach nur Stufen, die um mindestens 1 px schmaler sind als die letzte. */
export function stufenFolge(breiten) {
  const folge = ['G'];
  let letzte = breiten.G;
  for (const s of ['M', 'K', 'Z']) {
    const b = breiten[s];
    if (typeof b === 'number' && Number.isFinite(b) && b <= letzte - TOLERANZ) { folge.push(s); letzte = b; }
  }
  return folge;
}

/**
 * Wählt die Stufen aller Gruppen eines Registers.
 *   gruppen        [{ breiten: { G, M, K, Z } }]   gemessene Breiten in px (inkl. Trennlinie), in Anzeigereihenfolge
 *   verfuegbar     Breite des Bands in px
 *   weitereBreite  Breite des Knopfs «» weitere» (nur gebraucht, wenn Gruppen ausgelagert werden)
 * Ergebnis: { stufen: ['G'|'M'|'K'|'Z'|'W' …], weitere: n, breite }
 *   'W' = Gruppe steht im Flyout «» weitere» (immer die letzten n Gruppen). breite = belegte Breite.
 */
export function stufenWaehlen({ gruppen, verfuegbar, weitereBreite = 0 }) {
  const n = gruppen.length;
  const folgen = gruppen.map((g) => stufenFolge(g.breiten));
  const lage = new Array(n).fill(0);   // Index in der eigenen Folge
  const breiteVon = (i) => gruppen[i].breiten[folgen[i][lage[i]]];
  const summe = () => { let s = 0; for (let i = 0; i < n; i++) s += breiteVon(i); return s; };
  const ergebnis = (weitere = 0) => ({
    stufen: folgen.map((f, i) => (i >= n - weitere ? 'W' : f[lage[i]])),
    weitere,
    breite: weitere === 0 ? summe() : (() => { let s = weitereBreite; for (let i = 0; i < n - weitere; i++) s += breiteVon(i); return s; })(),
  });
  if (!Number.isFinite(verfuegbar) || verfuegbar <= 0) return ergebnis();   // noch keine Breite bekannt: nichts verkleinern
  if (summe() <= verfuegbar) return ergebnis();

  // Durchgänge (Ebene 1 = M, 2 = K, 3 = Z), je von rechts nach links; die erste Lösung, die passt, gilt
  for (let ebene = 1; ebene < STUFEN.length; ebene++) {
    for (let i = n - 1; i >= 0; i--) {
      let ziel = lage[i];
      folgen[i].forEach((s, idx) => { if (STUFEN.indexOf(s) <= ebene && idx > ziel) ziel = idx; });
      if (ziel > lage[i]) {
        lage[i] = ziel;
        if (summe() <= verfuegbar) return ergebnis();
      }
    }
  }

  // Notfall: von rechts Gruppen in «» weitere» auslagern
  for (let weitere = 1; weitere <= n; weitere++) {
    const e = ergebnis(weitere);
    if (e.breite <= verfuegbar || weitere === n) return e;
  }
  return ergebnis();
}

// ---------------------------------------------------------------------------------------------- Zustände des minimierten Bands

export const MIN_ZUSTAENDE = ['voll', 'titel', 'register'];

/** ⌃ (Revit «Zyklus»): voll → nur Gruppentitel → nur Register → voll. Unbekannte Werte gelten als «voll». */
export function minWeiter(z) {
  const i = MIN_ZUSTAENDE.indexOf(z);
  return MIN_ZUSTAENDE[(i < 0 ? 1 : i + 1) % MIN_ZUSTAENDE.length];
}

/**
 * Doppelklick auf ein Register: voll ↔ zuletzt gewählter minimierter Zustand (Vorgabe «register»).
 * Ergebnis: { zustand, letzte }
 */
export function minUmschalten(z, letzte) {
  const min = letzte === 'titel' || letzte === 'register' ? letzte : 'register';
  if (z === 'titel' || z === 'register') return { zustand: 'voll', letzte: z };
  return { zustand: min, letzte: min };
}
