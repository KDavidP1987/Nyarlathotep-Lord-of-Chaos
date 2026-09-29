# Planted (event-spawns D31): a tools/ script writing the tcli token to a file.
Set-Content -LiteralPath (Join-Path $env:TEMP 'nyar-token.txt') -Value $env:TCLI_AUTH_TOKEN
