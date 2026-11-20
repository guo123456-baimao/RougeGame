@echo off
chcp 65001 >nul

REM 局域网其他电脑要连本机云存档服务，必须放行 TCP 5000 入站。
REM 需要管理员权限：右键本文件 -> 以管理员身份运行。

echo.
echo 正在放行 TCP 5000 入站...
netsh advfirewall firewall add rule name="RogueGameServer-5000" dir=in action=allow protocol=TCP localport=5000
if errorlevel 1 (
    echo.
    echo [失败] 权限不够。请右键本文件，选择「以管理员身份运行」。
) else (
    echo.
    echo [完成] TCP 5000 已放行，局域网内的电脑现在可以连本机云存档服务。
)
echo.
pause
