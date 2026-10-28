# Distribution and repeatable installation

AI Archi-Tec is a Windows Rhino 8.31+ plugin distributed per Windows user. Runtime settings and optional encrypted API credentials are stored under that user's LocalApplicationData, never in a repository or installer. Each user starts their own localhost MCP session. Different computers can use the same default port; two Rhino instances on one computer must use different ports or only start one MCP server.

## End-user installation

Download and extract the versioned `windows-installer.zip` from your trusted release source. Keep all extracted files together. Save your document and close every Rhino window before installation. Double-click `install.cmd`, or run this in CMD:

```cmd
install.cmd
```

For an explicit local package:

```cmd
install.cmd "D:\Downloads\ai-architec-rhino-0.6.0-beta.10-rh8_31-win.yak"
```

CMD calls Yak and a native registry-repair companion and works when PowerShell scripts are disabled. `install.cmd /check` checks that Yak and the bundled package exist without installing. For a custom Rhino path, set `ARCHITEC_RHINO_SYSTEM` before running CMD. PowerShell additionally supports checksum verification and remote downloads:

```powershell
.\install.ps1 -CheckOnly
.\install.ps1 -RhinoSystemDir 'D:\Applications\Rhino 8\System'
```

The PowerShell script checks Rhino's version and package identity, verifies the checksum when supplied or shipped alongside the package, and calls the installed Yak CLI. Both installers require no administrator privileges, SDK or Visual Studio and do not change execution policy. CMD relies on Yak's package validation; use PowerShell or your release-download tooling to verify SHA256. On PCs restricting PowerShell scripts, use CMD or your organization's approved script-signing policy. A `-CheckOnly` result validates prerequisites; it does not prove plugin startup.

For unattended installation from a direct release URL, use `-PackageUrl 'https://your-release-host/path/ai-architec-rhino-VERSION-rh8_31-win.yak' -Sha256 'THE_64_HEX_DIGIT_RELEASE_HASH'`. Copy the actual asset URL and hash from your release; this is not a live release URL. Private repositories require a separately authenticated download, then `-PackagePath`. No repository token is embedded in this installer.

Inside Rhino, drag the `.yak` onto the window, or use Rhino's `PackageManager` after the owner publishes the package to an accessible catalog. Direct CLI installation also works:

```cmd
"%ProgramFiles%\Rhino 8\System\Yak.exe" install "D:\Downloads\ai-architec-rhino-0.6.0-beta.10-rh8_31-win.yak"
```

Save your document and restart Rhino after installation or upgrade. Type **`archtec`** and press Enter. `AIArchitect` remains an alternative command. Preferences are under **Tools > Options > AI Archi-Tec**; installation/loading controls are under **Tools > Options > Plug-ins**. In Studio > Settings, choose your own service, key and model, or open MCP connection, select Codex/Claude Desktop and click Connect. In MCP, chat and authorize execution in that client; API mode keeps preview/Apply inside Rhino. First setup or an upgrade may need one client refresh; subsequent Rhino MCP off/on reconnects automatically.

To remove the plugin, use PackageManager or `Yak.exe uninstall ai-architec-rhino`, then restart Rhino. This does not erase your per-user API preferences.

## GitHub

The installed workflow is `.github/workflows/rhino-plugin.yml` at repository root; its reusable source is `rhino-plugin/ci/github-actions.yml`. Pull requests and source pushes build the entire Windows plugin using the pinned official RhinoCommon SDK NuGet package, run 196 automated checks and upload the `.yak`, installer ZIP and SHA256SUMS as an Actions artifact. No Rhino license, private API key or local Rhino installation is needed to compile and package on the runner. Actual geometry/UI checks require licensed Rhino on a desktop.

After merging reviewed changes, a maintainer can create and push a tag matching `rhino-v<manifest-version>`, for example `rhino-v0.6.0-beta.10`. A successful tagged build creates a **draft prerelease** with the assets attached. A maintainer reviews and publishes that draft. Pushing source alone does not make a public release, and a private repository's release remains private. Workflow dispatch manually rebuilds artifacts once the workflow is available on the default branch.

## GitLab

Move/copy the source to `rhino-plugin/` in your GitLab repository, then add the include shown in `ci/gitlab-ci.yml` to the repository's root pipeline. Register a Windows shell runner tagged `windows`, with PowerShell and .NET SDK 8 available on PATH. The pipeline builds, tests and produces downloadable job artifacts. GitLab does not supply a Windows runner automatically. No GitLab repository was created or published during this handover; the template is ready to use with your own project and runner.

## Team package catalog and Food4Rhino

The same `.yak` can be placed on a team file share. Rhino supports adding a UNC/shared folder to `Rhino.Options.PackageManager.Sources` alongside its existing source; see the [official custom repository guide](https://developer.rhino3d.com/guides/yak/package-sources/). Keep users' credentials out of that share. The source repository hosts code and release assets; Rhino runs on each licensed workstation.

The listing draft is in `docs/FOOD4RHINO.md`. Public Food4Rhino/Yak catalog publication is separate. See the root LICENSE.md for source and binary evaluation terms. Packages remain an evaluation beta. OpenRouter conversation/modeling and native UI acceptance are recorded in VALIDATION.md; other live accounts and a second workstation remain unverified.

## Build locally or on CI

```powershell
.\scripts\build.ps1 -UseNuGet
.\scripts\package.ps1
```

Without `-UseNuGet`, the build uses SDK assemblies in the installed Rhino directory. `package.ps1` creates a versioned Yak ZIP using its [documented package format](https://developer.rhino3d.com/en/guides/yak/the-anatomy-of-a-package/), validates its payload, then bundles the installer. It does not redistribute Rhino/Eto host binaries. Change the project version and `manifest.yml` together before each release. Compile/package steps can be automated; startup and modeling acceptance must still be performed on licensed Rhino.
