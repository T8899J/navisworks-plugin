@echo off
title 安装 Visual Studio 2022 Build Tools

echo ============================================
echo  安装 Visual Studio 2022 Build Tools
echo  请以管理员身份运行本脚本！
echo ============================================
echo.

REM 检查管理员权限
net session >nul 2>nul
if %errorlevel% neq 0 (
    echo 错误：请以管理员身份运行本脚本！
    echo 右键点击本文件 - "以管理员身份运行"
    pause
    exit /b 1
)

echo 管理员权限确认。
echo 正在下载安装程序（约 4 MB）...
echo.

set INSTALLER=%TEMP%\vs_BuildTools.exe
powershell -Command "(New-Object System.Net.WebClient).DownloadFile('https://aka.ms/vs/17/release/vs_BuildTools.exe', '%INSTALLER%')"

if not exist "%INSTALLER%" (
    echo 下载失败！
    pause
    exit /b 1
)

echo 下载完成，正在安装...
echo 安装过程需要下载组件（约 2-3 GB），请耐心等待 10-30 分钟。
echo.

"%INSTALLER%" --quiet --wait --norestart --nocache --add Microsoft.VisualStudio.Workload.MSBuildTools --add Microsoft.Net.Component.4.8.SDK --includeRecommended

set RESULT=%errorlevel%

echo.
if %RESULT% equ 0 (
    echo ============================================
    echo  安装成功！
    echo ============================================
    echo  现在可以编译插件。
) else if %RESULT% equ 3010 (
    echo ============================================
    echo  安装完成，请重启计算机。
    echo ============================================
) else (
    echo ============================================
    echo  安装失败（退出码: %RESULT%）
    echo  请尝试手动安装：
    echo  1. 打开 https://aka.ms/vs/17/release/vs_BuildTools.exe
    echo  2. 以管理员身份运行
    echo  3. 勾选 ".NET desktop build tools"
    echo  4. 等待安装完成
    echo ============================================
)

pause
