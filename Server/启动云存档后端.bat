@echo off
chcp 65001 >nul
setlocal

REM 云存档后端启动脚本：双击即可启动 Unity 客户端依赖的服务。
REM 关掉这个窗口，服务就停了。

cd /d "%~dp0RogueGameServer"

echo.
echo [1/3] 检查 MySQL 服务...
sc query mysql80 | findstr /C:"RUNNING" >nul
if errorlevel 1 (
    echo       MySQL 没有在运行，正在尝试启动（需要管理员权限）...
    net start mysql80 >nul 2>&1
    if errorlevel 1 (
        echo       [失败] 启动不了 MySQL。请右键「以管理员身份运行」这个脚本，^
或手动在服务里启动 mysql80。
        echo.
        pause
        exit /b 1
    )
)
echo       MySQL 正常。

echo.
echo [2/3] 构建后端...
dotnet build -v q --nologo
if errorlevel 1 (
    echo       [失败] 构建没过，请看上面的报错。
    echo       常见原因：上一个服务进程还占着 bin 目录里的 exe，^
先关掉那个黑窗口再重试。
    echo.
    pause
    exit /b 1
)
echo       构建通过。

echo.
echo [3/4] 放行防火墙 5000 端口（局域网其他机器要连进来，必须放行）...
netsh advfirewall firewall show rule name="RogueGameServer-5000" >nul 2>&1
if errorlevel 1 (
    netsh advfirewall firewall add rule name="RogueGameServer-5000" dir=in action=allow protocol=TCP localport=5000 >nul 2>&1
    if errorlevel 1 (
        echo       [提示] 自动放行失败，一般是权限不够。
        echo             请右键「以管理员身份运行」Server 目录下的
        echo             「放行防火墙5000端口.bat」，只做一次即可。
    ) else (
        echo       已放行 TCP 5000 入站。
    )
) else (
    echo       规则已存在，跳过。
)

echo.
echo [4/4] 启动服务...
echo.
echo       本机       http://localhost:5000
echo       健康检查   http://localhost:5000/healthz
echo.
echo       本机局域网地址（把和对方同一网段的那个填进游戏目录的 server.json）：
for /f "tokens=2 delims=:" %%i in ('ipconfig ^| findstr /R /C:"IPv4"') do echo          %%i
echo.
echo       要停止服务，直接关闭这个窗口。
echo.

dotnet run --project . --no-build

echo.
echo 服务已停止。
pause
