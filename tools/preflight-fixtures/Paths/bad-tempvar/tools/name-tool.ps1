# A fixture tool: its scratch folder's name is wholly a variable, so no literal names it and no registration covers it.
param([string]$Name = 'scratch')
$scratch = Join-Path $env:TEMP $Name
