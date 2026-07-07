# 傑出品 Navisworks 多版本构建

支持 2017-2026 共 10 个 Navisworks 版本的统一构建系统。
零侵入设计 — 不修改原项目任何文件，通过 `<Compile Include="..\*.cs" />` 复用源码。

## 快速开始

```
# 构建
build.bat           # 默认 2023
build.bat 2021      # 构建 2021 版本

# 打包（构建 + 生成可分发安装包）
package.bat         # 输出到 release\ 文件夹

# 部署（本机）
install.bat 2023    # 安装到本机 Navisworks 2023
```

## 发布到其他电脑

1. 运行 `package.bat` 生成 `release\` 文件夹
2. 将整个 `release\` 文件夹复制到 U 盘
3. 在目标电脑上运行 `release\安装.bat`
4. 安装程序自动查找 Navisworks，复制 DLL 和清单
5. 重启 Navisworks 即可使用

## 目录结构

```
multi-version/
├── NavisworksPlugin.Multi.csproj  # 多版本 .csproj
├── build.bat                      # 构建脚本
├── package.bat                    # 打包脚本
├── install.bat                    # 本机安装
├── scripts/
│   ├── install.ps1                # 高级安装（支持 -AllVersions）
│   └── install-standalone.bat     # 独立安装器（复制到 release\）
├── manifests/                     # 按版本命名的清单文件
│   └── 傑出品NavisworksPlugin_2021.plugin
├── release/                       # 打包输出（可分发）
│   ├── 傑出品NavisworksPlugin.dll
│   ├── 傑出品NavisworksPlugin_2021.plugin
│   └── 安装.bat
└── Polyfills/                     # .NET 兼容补丁
    └── IsExternalInit.cs
```
