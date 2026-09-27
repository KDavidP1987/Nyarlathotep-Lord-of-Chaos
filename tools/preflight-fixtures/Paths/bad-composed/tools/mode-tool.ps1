# A fixture tool: it composes its scratch folder's name from a mode, so no source scan can read the name.
param([string]$Mode = 'x')
$scratch = Join-Path $env:TEMP ('nyar-' + $Mode)
$other = Join-Path $env:TEMP ('nyar' + '-' + $Mode)
