# Rendert Seite 1 eines PDF mit Windows.Data.Pdf (eingebaut) zu PNG. Nur ASCII.
param([string]$Pdf, [string]$Png, [int]$Breite = 2400)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
$null = [Windows.Data.Pdf.PdfDocument, Windows.Data.Pdf, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.InMemoryRandomAccessStream, Windows.Storage.Streams, ContentType = WindowsRuntime]
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
$asTaskAktion = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncAction' })[0]
function Warten($op, [Type]$typ) { $t = $asTask.MakeGenericMethod($typ).Invoke($null, @($op)); $t.Wait(); $t.Result }
$datei = Warten ([Windows.Storage.StorageFile]::GetFileFromPathAsync($Pdf)) ([Windows.Storage.StorageFile])
$dok = Warten ([Windows.Data.Pdf.PdfDocument]::LoadFromFileAsync($datei)) ([Windows.Data.Pdf.PdfDocument])
$seite = $dok.GetPage(0)
$opt = New-Object Windows.Data.Pdf.PdfPageRenderOptions
$opt.DestinationWidth = $Breite
$strom = New-Object Windows.Storage.Streams.InMemoryRandomAccessStream
$asTaskAktion.Invoke($null, @($seite.RenderToStreamAsync($strom, $opt))).Wait()
$netz = [System.IO.WindowsRuntimeStreamExtensions]::AsStreamForRead($strom.GetInputStreamAt(0))
$aus = [System.IO.File]::Create($Png); $netz.CopyTo($aus); $aus.Close()
"Seiten: $($dok.PageCount), Seite 1: $($seite.Size.Width) x $($seite.Size.Height) pt -> $Png"
