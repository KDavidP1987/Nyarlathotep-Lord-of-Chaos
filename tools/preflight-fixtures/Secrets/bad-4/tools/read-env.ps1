# Reads a token from the environment drive (planted fault).
$t = (Get-Item -LiteralPath env:GH_TOKEN).Value
Write-Host $t.Length
