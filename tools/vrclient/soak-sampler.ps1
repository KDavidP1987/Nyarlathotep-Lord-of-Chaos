# soak-sampler.ps1: apocalypse D30's record check. Every -Every seconds appends "<utc now> <state.json last write utc>"
# to -Out until -Hours have passed; the orchestrator then reads the largest gap between successive distinct write times.
param([string]$State = 'C:\Program Files (x86)\Steam\steamapps\common\VRisingDedicatedServer\BepInEx\config\Nyarlathotep\state.json',
      [string]$Out, [int]$Every = 20, [double]$Hours = 3.5)
$end = (Get-Date).AddHours($Hours)
while ((Get-Date) -lt $end) {
    $w = (Get-Item -LiteralPath $State).LastWriteTimeUtc.ToString('o')
    Add-Content -LiteralPath $Out -Value ("{0} {1}" -f (Get-Date).ToUniversalTime().ToString('o'), $w)
    Start-Sleep -Seconds $Every
}
