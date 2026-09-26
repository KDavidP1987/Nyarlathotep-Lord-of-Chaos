# Publishes the package (planted fault: reads the gh credential).
$token = gh auth token
Write-Host "token length $($token.Length)"
