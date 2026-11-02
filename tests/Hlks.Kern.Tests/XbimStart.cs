namespace Hlks.Kern.Tests;

/// <summary>Die Tests erzeugen IFC-Dateien selbst mit xBIM (IfcStore.Create): zuerst die Dienste von KICAD samt Geometrie einrichten, sonst baut
/// xBIM sie ohne Geometrie und die xBIM-Geometrie fiele aus (IfcGeometrieXbim.DiensteEinrichten).</summary>
static class XbimStart
{
#pragma warning disable CA2255
    [System.Runtime.CompilerServices.ModuleInitializer]
#pragma warning restore CA2255
    internal static void Einrichten() => IfcGeometrieXbim.DiensteEinrichten();
}
