@echo off
:loop
C:\Users\danyd\tiaportal-mcp\src\TiaMcpServer\bin\Debug\net48\TiaMcpServer.exe --logging 1
if errorlevel 1 timeout /t 2 /nobreak >nul
goto loop
