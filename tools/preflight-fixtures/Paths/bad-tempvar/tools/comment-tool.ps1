param([string]$Name = 'scratch')
$scratch = Join-Path $env:TEMP $Name   # not nyar-session, whatever this comment says
