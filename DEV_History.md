# DEV History — Phantombite StationRefill

## 2026-09-19 — Bereinigung
- **Fehler behoben:** Bei Performance-Level 1 wurde die Auffüll-Warteschlange gefüllt, aber nie abgearbeitet.
  Es wurde dann gar nichts aufgefüllt. Die Queue wird jetzt pro Tick mit 5 Aufgaben verarbeitet.
- Die Config wird nur noch auf dem Server angelegt und gelesen.
- Doku angelegt, `.gitignore` ergänzt.
