# SophonDownloader

Download miHoYo game assets using their new download method

[English][p:en-us] | [中文][p:zh-cn]

---

After Genshin forced SophonChunks to update and stopped giving zip files for updates in version 5.6, it was no longer possible to download game assets without using HoYoPlay.

> If you encounter any problem, please report an Issue.

---

## Download and Installation

### Use Pre-built

- [Click here for latest build](https://github.com/DangoRepo/SophonDownloader/actions/runs/15238865972/artifacts/3193171693) ✨

### Build From Source

To build, you need .NET 9.0 or higher. If you've installed .NET but have no idea about the version, you can type `dotnet --version` in Command Prompt to check it out.

1. **Clone the repository**

    - Fetch using SSH：

    ``` cmd
    git clone git@github.com:DangoRepo/SophonDownloader.git
    ```

    - Fetch using HTTPS：

    ``` cmd
    git clone https://github.com/DangoRepo/SophonDownloader.git
    ```

2. **Build**

After cloning the repository, open Command Prompt in folder `Sophon.Downloader`, then run:

``` cmd
dotnet build Sophon.Downloader.sln -c Release
```

The app should be in `Sophon.Downloader/Core/bin/Release/netx.x`.

---

## How to use

```
Usage:
    Sophon.Downloader.exe full <gameId> <package> <version> <outputDir> [options]                     Download full game assets
    Sophon.Downloader.exe update <gameId> <package> <updateFrom> <updateTo> <outputDir> [options]     Download update assets

Arguments:
    <gameId>        Game ID, either hoyo id (hk4e, hkrpg, nap, bh2) or REL id (gopR6Cufr3, ...)
    <package>       What to download, either "game" or for audio "zh-cn", "en-us", "ja-jp" or "ko-kr"
    <version>       Version to download
    <updateFrom>    Version to update from
    <updateTo>      Version to update to
    <outputDir>     Output directory to save the downloaded files

Options:
    --region=<value>            Region to use, either OSREL (overseas) or CNREL (china), defaults to OSREL
    --branch=<value>            Override branch name of the game data, if you want to download predownload package you must input "predownload"
    --launcherId=<value>        Override launcher ID used when fetching packages
    --platApp=<value>           Override platform application ID used when fetching packages
    --threads=<value>           Number of threads to use, defaults to the number of processors
    --handles=<value>           Number of HTTP handles to use, defaults to 128
    --silent                    Suppress confirmation message and output
    -h, --help                  Show this help message
```

---

## Game ID

| Game | ID |
| - | - |
| Honkai Impact 3rd | `bh3` |
| Genshin Impact | `hk4e` |
| Honkai: Star Rail | `hkrpg` |
| Zenless Zone Zero | `nap` |

---

## Credits

- [Hi3Helper.Sophon](https://github.com/CollapseLauncher/Hi3Helper.Sophon) - Sophon assets management

[p:en-us]: README.md
[p:zh-cn]: README_zh-cn.md
