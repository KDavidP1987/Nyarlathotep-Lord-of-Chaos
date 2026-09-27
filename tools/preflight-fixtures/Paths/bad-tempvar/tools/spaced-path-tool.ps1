param([string]$Name = 'scratch')
$scratch = Join-Path ([IO.Path]::GetTempPath( )) $Name
