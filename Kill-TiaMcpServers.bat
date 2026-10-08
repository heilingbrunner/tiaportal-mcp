@echo off

powershell.exe -ExecutionPolicy Bypass -NonInteractive -Command "$p = Get-Process -Name TiaMcpServer -ErrorAction SilentlyContinue; if (-not $p) { Write-Host 'No running kanban.exe processes found.' } else { $p | ForEach-Object { try { Stop-Process -Id $_.Id -Force -ErrorAction Stop; Write-Host ('Stopped kanban.exe (PID ' + $_.Id + ').') } catch { Write-Warning ('Could not stop PID ' + $_.Id + ': ' + $_.Exception.Message) } } }"
