// «Kälte ist Teil der Heizung mit anderem Medium» (Entscheid Christian Steiner, 20.10.2026, entwurf/gewerk-kaelte.md): Kälte ist kein eigenes
// Gewerk. Ältere Werte «kaelte» (Projekte, Benutzereinstellungen, Gewerklisten von Ansichten) werden beim Lesen als «heizung» gelesen – nie ein
// Fehler. Spiegel von KaelteAlias.Gewerk im Kern (src/Hlks.Kern/Kataloge/KaelteAlias.cs). Rein, ohne DOM.

/** Frühere Gewerk-Werte → heutiges Gewerk. */
export const GEWERK_ALIAS = { kaelte: 'heizung', 'kälte': 'heizung' };

/** Das heutige Gewerk zu einem (möglicherweise alten) Wert; alles andere unverändert. */
export const gewerkAlias = (g) => (typeof g === 'string' ? (GEWERK_ALIAS[g.trim().toLowerCase()] ?? g) : g);

/** Liste von Gewerken mit dem Alias gelesen, ohne Doppelte (Reihenfolge bleibt). */
export const gewerkeAlias = (liste) => [...new Set((Array.isArray(liste) ? liste : []).map(gewerkAlias))];
