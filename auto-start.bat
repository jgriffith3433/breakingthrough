@echo off
cd /d "%~dp0"
if not exist node_modules (
  echo Installing the local server...
  call npm install
)
echo Breaking Through: http://localhost:8089/Game/
start "" "http://localhost:8089/Game/"
call npm run dev -- --host 0.0.0.0 --port 8089
