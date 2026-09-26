# Allowed environment reads only: the plain and the braced variable form.
$scratch = Join-Path $env:TEMP "nyar"
$other = ${env:TEMP}
$env:SteamAppId = "1604030"
