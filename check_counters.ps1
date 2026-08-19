Get-Counter -ListSet * | ForEach-Object {
    if ($_.CounterSetName -match 'memory|memoria') {
        Write-Host $_.CounterSetName
        $_.Counter | ForEach-Object { Write-Host "  $_" }
    }
}
