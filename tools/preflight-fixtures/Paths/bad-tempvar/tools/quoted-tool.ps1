param([string]$Name = 'scratch')
$scratch = Join-Path $env:TEMP $Name; $note = '# nyar-temp: a string, not a comment'
