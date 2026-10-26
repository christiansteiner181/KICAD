@echo off
rem Netz fuer die Brille einrichten (KICAD und XR-Labor): Doppelklick. Details in xr_netz_einrichten.ps1.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0xr_netz_einrichten.ps1" %*
