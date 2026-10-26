# MediaPipe Tasks Vision (Handerkennung für Modul G «Gesten am Arbeitsplatz»)

Lokale Kopie, damit KICAD ohne Internet läuft (kein CDN zur Laufzeit). Geladen nur vom Worker `js/gesten/gesten-worker.js`
(Modul-Worker), siehe `entwurf/modul-gesten.md`, Abschnitt «Technik».

| Datei | Herkunft | Grösse (Byte) | SHA-256 |
|---|---|---|---|
| `vision_bundle.mjs` | npm `@mediapipe/tasks-vision@1.1.1`, Datei `vision_bundle.mjs` | 156 139 | `a84921e02321e582484c1f8126d344b5f33b791fa09c640a52e20ee8632068f1` |
| `wasm/vision_wasm_module_internal.js` | dito, `wasm/vision_wasm_module_internal.js` | 334 166 | `539a4386fd12d0723d6e900ec80467d168ba7290dda2f79e77a2181b8e906115` |
| `wasm/vision_wasm_module_internal.wasm` | dito, `wasm/vision_wasm_module_internal.wasm` | 13 519 940 | `3b57e4b55ac6346bf940212e3e11250b308a6a474077385e2f527b119cf74525` |
| `hand_landmarker.task` | `https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task` (Stand 26.04.2023, ETag/MD5 `15318430ea3851670fe9914116a9cfad`) | 7 819 105 | `fbc2a30080c3c557093b5ddfc334698132eb341044ccee322ccf8bcf3607cde1` |
| `LICENSE` | Apache License 2.0, `https://www.apache.org/licenses/LICENSE-2.0.txt` | 11 358 | `cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30` |

- Paket geholt am 26.10.2026 mit `npm pack @mediapipe/tasks-vision@1.1.1` (Tarball-SHA-1 `9a219124017db10bbeb4aedca86d367a640c555d`, wie im
  npm-Verzeichnis). Lizenz laut `package.json`: Apache-2.0. Das Paket enthält keine eigene LICENSE-Datei; beigelegt ist der Wortlaut der Apache
  License 2.0. Das Modell `hand_landmarker.task` steht laut MediaPipe ebenfalls unter Apache 2.0.
- Übernommen ist nur die **Modul-Variante mit SIMD** (`vision_wasm_module_internal.*`): der Worker ist ein Modul-Worker und lädt den WASM-Lader per
  `import()` (`FilesetResolver.forVisionTasks('/lib/mediapipe/wasm', true)`). Edge/WebView2 kann SIMD; die Variante ohne SIMD
  (`vision_wasm_nosimd_internal.*`) und die klassische (`vision_wasm_internal.*`, `importScripts`) sind nicht nötig und fehlen bewusst. Die Quellkarte
  `vision_bundle.mjs.map` fehlt ebenfalls (nur für die Fehlersuche; der Verweis am Ende des Bündels bleibt unverändert).
- **Datenschutz:** Version 1.1.1 meldet Nutzungsdaten (Aufgabe, Laufmodus, Zeiten) per `fetch` an `https://odml.pa.googleapis.com/v1/log`
  (Befund 26.10.2026 im Bündel, Klasse mit `x-goog-api-key`). KICAD lässt das nicht zu: Der Worker sperrt jede Netzanfrage ausser auf
  `/lib/mediapipe/` desselben Ursprungs (`netzErlaubt` in `js/gesten/gesten-logik.js`), und der Server liefert den Worker mit
  `Content-Security-Policy: connect-src 'self'` aus. Kamerabilder verlassen den Browser nie.
- Aktualisieren: neue Version holen, nur diese Dateien ersetzen, Grössen und Prüfsummen hier nachführen, `gesten.browser.mjs` laufen lassen und
  im Bündel erneut nach Netzadressen suchen (`https://`).
