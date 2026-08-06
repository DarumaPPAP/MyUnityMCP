@echo off
setlocal
cd /d "%~dp0\..\.."
python tools\graph-viewer\server.py
if errorlevel 1 (
  echo.
  echo Graph Dashboardの起動に失敗しました。
  echo Python 3がPATHに登録されているか確認してください。
  pause
)
