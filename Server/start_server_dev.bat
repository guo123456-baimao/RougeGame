@echo off
rem 以 Development 环境启动 RogueGameServer（与 launchSettings.json 等效）
set ASPNETCORE_ENVIRONMENT=Development
cd /d "%~dp0RogueGameServer\bin\Debug\net9.0"
RogueGameServer.exe
