<div align="center">

# 🏭 TIA Portal MCP Server

### Bridge AI Coding Assistants with Siemens TIA Portal

[![.NET Framework](https://img.shields.io/badge/.NET_Framework-4.8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet-framework/net48)
[![TIA Portal](https://img.shields.io/badge/TIA_Portal-V18--V20+-00A98F?style=for-the-badge&logo=siemens&logoColor=white)](https://www.siemens.com/tia-portal)
[![MCP Protocol](https://img.shields.io/badge/MCP-Protocol-FF6B35?style=for-the-badge)](https://modelcontextprotocol.io)
[![License](https://img.shields.io/github/license/hemangjoshi37a/hjLabs.in-tiaportal-mcp?style=for-the-badge)](LICENSE.txt)
[![Stars](https://img.shields.io/github/stars/hemangjoshi37a/hjLabs.in-tiaportal-mcp?style=for-the-badge&color=yellow)](https://github.com/hemangjoshi37a/hjLabs.in-tiaportal-mcp/stargazers)

<br/>

**The first open-source MCP server that enables AI assistants like Claude, GitHub Copilot, and others to directly program Siemens PLCs through TIA Portal Openness API.**

*Create, modify, compile, and download PLC programs using natural language.*

<br/>

[Getting Started](#-getting-started) · [Features](#-features) · [Tools](#-mcp-tools-90) · [Configuration](#-configuration) · [Contributing](#-contributing) · [Contact](#-contact)

<br/>

---

</div>

## 🎯 What is this?

TIA Portal MCP Server is a **Model Context Protocol (MCP)** server that acts as a bridge between LLM-based coding assistants and **Siemens TIA Portal**. It exposes **90+ tools** that let AI assistants:

- 🔌 **Connect** to running TIA Portal instances
- 📁 **Open, save, and manage** PLC projects
- 📝 **Create, export, and import** program blocks (OB, FB, FC, DB) in LAD, SCL, or STL
- 🏷️ **CRUD PLC tags** with full tag table management
- ⚙️ **Configure hardware** — devices, modules, networks, IP addresses
- 🖥️ **Manage HMI** — screens, tags, connections, alarms, text lists
- 📊 **Monitor and compare** — cross-references, online/offline compare
- 📚 **Library management** — project and global libraries
- 🔒 **Safety programming** — F-blocks, safety settings
- ⬇️ **Compile and download** programs to physical PLCs
- 🌐 **Go online/offline** for live debugging

> **Think of it as giving Claude or Copilot the ability to sit at the TIA Portal and program your PLC for you.**

<br/>

## ✨ Features

<table>
<tr>
<td width="50%">

### 🔧 PLC Programming
- Export/Import blocks (XML & SIMATIC SD)
- SCL, LAD, FBD, STL support
- Block CRUD (create, copy, move, delete)
- Block group management
- External source compilation
- Cross-reference analysis

</td>
<td width="50%">

### 🏷️ Tag Management
- PLC tag table CRUD
- Create/delete individual tags
- Export/import tag tables
- Watch table support
- Full regex filtering

</td>
</tr>
<tr>
<td>

### ⚡ Hardware & Network
- Device creation (by order number)
- Module configuration
- I/O address mapping
- Subnet management
- PROFINET/IP configuration
- GSD file import

</td>
<td>

### 🖥️ HMI Engineering
- Screen export/import
- HMI tag management
- PLC-HMI connections
- Alarm configuration
- Text list management

</td>
</tr>
<tr>
<td>

### 📡 Online Access
- Go online/offline
- Download to PLC
- Upload from PLC
- Live monitoring
- Offline/online compare

</td>
<td>

### 📚 Advanced
- Project/global libraries
- Technology objects (motion, PID)
- Safety programming (F-blocks)
- Multi-user project support
- Compile with error details

</td>
</tr>
</table>

<br/>

## 🛠️ MCP Tools (90+)

<details>
<summary><b>🔌 Connection & Project Management (8 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `Connect` | Connect to running TIA Portal or start new instance |
| `Disconnect` | Disconnect from TIA Portal |
| `GetState` | Get server connection state |
| `GetProject` | Get current project info |
| `OpenProject` | Open a TIA Portal project (.ap20) |
| `SaveProject` | Save current project |
| `SaveAsProject` | Save project with new name/location |
| `CloseProject` | Close current project |

</details>

<details>
<summary><b>📁 Project Structure (5 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `GetProjectTree` | Get full project tree (devices, software, hardware) |
| `GetSoftwareTree` | Get PLC software tree (blocks, types, sources) |
| `GetDevices` | List all devices in project |
| `GetDeviceInfo` | Get device details and attributes |
| `GetDeviceItemInfo` | Get device item (module) details |

</details>

<details>
<summary><b>📝 Program Blocks (15 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `GetBlocks` | List all blocks with regex filtering |
| `GetBlocksWithHierarchy` | List blocks with group paths |
| `GetBlockInfo` | Get block details (type, language, consistency) |
| `ExportBlock` | Export single block to XML |
| `ExportBlocks` | Bulk export blocks with regex filter |
| `ImportBlock` | Import block from XML |
| `DeleteBlock` | Delete a block |
| `CopyBlock` | Copy block to another group |
| `MoveBlock` | Move block between groups |
| `ExportAsDocuments` | Export as SIMATIC SD (.s7dcl/.s7res) |
| `ExportBlocksAsDocuments` | Bulk export as documents |
| `ImportFromDocuments` | Import from SIMATIC SD (V20+) |
| `ImportBlocksFromDocuments` | Bulk import from documents (V20+) |
| `CreateBlockGroup` | Create block folder |
| `DeleteBlockGroup` | Delete block folder |

</details>

<details>
<summary><b>🏷️ Tags & Watch Tables (10 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `GetTagTables` | List PLC tag tables |
| `GetTagTable` | Get tag table with all tags |
| `GetTags` | Get tags with regex filtering |
| `CreateTag` | Create new PLC tag |
| `DeleteTag` | Delete a PLC tag |
| `ExportTagTable` | Export tag table to XML |
| `ImportTagTable` | Import tag table from XML |
| `GetWatchTables` | List watch/force tables |
| `ExportWatchTable` | Export watch table |
| `ImportWatchTable` | Import watch table |

</details>

<details>
<summary><b>⚡ Hardware & Network (11 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `CreateDevice` | Create new device (PLC, HMI) |
| `DeleteDevice` | Remove device from project |
| `CreateDeviceGroup` | Create device group |
| `GetModules` | List modules in a device |
| `GetModuleInfo` | Get module details |
| `GetAddresses` | Get I/O address ranges |
| `GetSubnets` | List network subnets |
| `GetNetworkInterfaces` | Get network interfaces |
| `SetIpAddress` | Configure IP address |
| `ConnectToSubnet` | Connect device to subnet |
| `ImportGsdFile` | Import GSD/GSDML file |

</details>

<details>
<summary><b>🖥️ HMI Engineering (15 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `GetHmiTagTables` | List HMI tag tables |
| `GetHmiTags` | Get HMI tags |
| `ExportHmiTagTable` | Export HMI tag table |
| `ImportHmiTagTable` | Import HMI tag table |
| `GetScreens` | List HMI screens |
| `GetScreenInfo` | Get screen details |
| `ExportScreen` | Export screen to XML |
| `ImportScreen` | Import screen from XML |
| `GetHmiConnections` | List HMI-PLC connections |
| `CreateHmiConnection` | Create HMI connection |
| `GetDiscreteAlarms` | List discrete alarms |
| `GetAnalogAlarms` | List analog alarms |
| `GetTextLists` | List text lists |
| `ExportTextList` | Export text list |
| `ImportTextList` | Import text list |

</details>

<details>
<summary><b>📡 Online & Download (6 tools)</b></summary>

| Tool | Description |
|------|-------------|
| `CompileSoftware` | Compile with detailed error messages |
| `GoOnline` | Establish online connection to PLC |
| `GoOffline` | Disconnect from PLC |
| `DownloadToDevice` | Download program to PLC |
| `CompareOfflineOnline` | Compare project vs PLC |
| `CompareBlocks` | Compare two blocks |

</details>

<details>
<summary><b>📚 Libraries & Advanced (20+ tools)</b></summary>

| Tool | Description |
|------|-------------|
| `GetProjectLibrary` | Browse project library |
| `GetGlobalLibraries` | List global libraries |
| `OpenGlobalLibrary` | Open a global library |
| `CopyToLibrary` | Copy block to library |
| `CopyFromLibrary` | Copy from library to project |
| `GetLibraryTypes` | List library types |
| `CreateProject` | Create new empty project |
| `GetMultiuserInfo` | Multi-user session info |
| `GetExternalSources` | List external source files |
| `ImportExternalSource` | Import SCL/AWL source |
| `GenerateBlocksFromSource` | Compile external source |
| `GetCrossReferences` | Find all usages of block/tag |
| `GetTechnologyObjects` | List motion/PID objects |
| `ExportTechnologyObject` | Export technology object |
| `GetSafetySettings` | Safety program settings |
| `GetSafetyBlocks` | List F-blocks |
| `SetSafetyPassword` | Set safety password |
| *...and more* | |

</details>

<br/>

## 🚀 Getting Started

### Prerequisites

| Requirement | Details |
|------------|---------|
| **OS** | Windows 10/11 (x64) |
| **Runtime** | .NET Framework 4.8 |
| **TIA Portal** | V18, V19, or V20 with Openness license |
| **User Group** | Must be member of `Siemens TIA Openness` Windows group |
| **Environment** | `TiaPortalLocation` env var set to TIA install path |

### Build & Run

```powershell
# Clone the repository
git clone https://github.com/hemangjoshi37a/hjLabs.in-tiaportal-mcp.git
cd hjLabs.in-tiaportal-mcp

# Build
dotnet build

# Run (TIA Portal V20)
dotnet run --project src/TiaMcpServer/TiaMcpServer.csproj -- --tia-major-version 20 --logging 1

# Run (TIA Portal V18)
dotnet run --project src/TiaMcpServer/TiaMcpServer.csproj -- --tia-major-version 18 --logging 1
```

### Logging Modes

| Flag | Output |
|------|--------|
| `--logging 1` | stderr (for stdio transport) |
| `--logging 2` | Visual Studio Debug / DebugView |
| `--logging 3` | Windows Event Log |

<br/>

## ⚙️ Configuration

### Claude Code (CLI)

Create `.mcp.json` in your project root:

```json
{
  "mcpServers": {
    "tiaportal-mcp": {
      "command": "dotnet",
      "args": [
        "run", "--project",
        "C:\\path\\to\\src\\TiaMcpServer\\TiaMcpServer.csproj",
        "--", "--tia-major-version", "20", "--logging", "1"
      ]
    }
  }
}
```

### Claude Desktop

Edit `%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "tiaportal-mcp": {
      "command": "C:\\path\\to\\TiaMcpServer.exe",
      "args": ["--tia-major-version", "20"],
      "env": {}
    }
  }
}
```

### VS Code Copilot Chat

Use the [TIA-Portal MCP-Server](https://marketplace.visualstudio.com/items?itemName=JHeilingbrunner.vscode-tiaportal-mcp) extension, or add to `.vscode/mcp.json`:

```json
{
  "servers": {
    "tiaportal-mcp": {
      "command": "C:\\path\\to\\TiaMcpServer.exe",
      "args": ["--tia-major-version", "20"],
      "env": {}
    }
  }
}
```

<br/>

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────┐
│                  AI Assistant                        │
│          (Claude / Copilot / ChatGPT)               │
└──────────────────┬──────────────────────────────────┘
                   │ MCP Protocol (stdio/JSON-RPC)
┌──────────────────▼──────────────────────────────────┐
│              TIA Portal MCP Server                   │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────┐ │
│  │  MCP Layer  │→ │ Portal Layer │→ │  Openness   │ │
│  │ (90+ Tools) │  │  (Wrapper)   │  │ (Siemens)   │ │
│  └─────────────┘  └──────────────┘  └──────┬─────┘ │
└─────────────────────────────────────────────┼───────┘
                                              │ COM/API
┌─────────────────────────────────────────────▼───────┐
│                  TIA Portal V20                      │
│     ┌─────────┐  ┌──────┐  ┌──────┐  ┌─────────┐  │
│     │   PLC   │  │ HMI  │  │ Libs │  │ Network │  │
│     │ Program │  │      │  │      │  │  Config │  │
│     └─────────┘  └──────┘  └──────┘  └─────────┘  │
└─────────────────────────────────────────────────────┘
```

**Three-layer design:**

1. **MCP Layer** — Static methods with `[McpServerTool]` attributes, handles protocol mapping
2. **Portal Wrapper** — Core wrapper around TIA Portal Openness API with standardized error handling
3. **Openness Init** — Assembly resolution for TIA Portal V13–V20+

<br/>

## 🔧 Use Cases

<table>
<tr>
<td width="33%" valign="top">

### 🏭 Industrial Automation
- Program S7-1200/1500 PLCs using natural language
- Auto-generate ladder logic from machine descriptions
- Bulk tag creation from I/O wiring lists
- Standardize PLC programs across machines

</td>
<td width="33%" valign="top">

### 🎓 Education & Training
- Learn PLC programming with AI guidance
- Interactive TIA Portal tutorials
- Code review for PLC programs
- Best practice enforcement

</td>
<td width="33%" valign="top">

### 🔄 DevOps for OT
- Version control PLC programs
- Automated block export/import
- CI/CD for PLC deployments
- Cross-reference auditing

</td>
</tr>
</table>

<br/>

## 📋 TIA Portal Version Support

| Feature | V18 | V19 | V20 |
|---------|:---:|:---:|:---:|
| Connect / Project Management | ✅ | ✅ | ✅ |
| Block Export/Import (XML) | ✅ | ✅ | ✅ |
| Tag Management | ✅ | ✅ | ✅ |
| Hardware Configuration | ✅ | ✅ | ✅ |
| HMI Engineering | ✅ | ✅ | ✅ |
| Export as Documents (.s7dcl) | ❌ | ❌ | ✅ |
| Import from Documents | ❌ | ❌ | ✅ |
| HMI Unified | ❌ | ✅ | ✅ |
| Compile with Error Details | ✅ | ✅ | ✅ |
| Online/Download | ✅ | ✅ | ✅ |

<br/>

## 🤝 Contributing

We welcome contributions! See [`AGENTS.md`](AGENTS.md) for guidance on working with agentic assistants and the test execution policy.

**Test execution policy:** Always offer to run tests and wait for explicit user confirmation. Tests require TIA Portal installed, licensed, and user in "Siemens TIA Openness" Windows group.

```powershell
# Run tests (requires TIA Portal + license)
dotnet test
```

<br/>

## 📄 Known Limitations

- Importing LAD blocks from SIMATIC SD documents requires `.s7res` to contain en-US tags
- `ExportBlock` requires fully qualified `blockPath` (e.g., `Group/Subgroup/Name`)
- TIA Portal does not export inconsistent (uncompiled) blocks
- Some HMI and Technology Object types require version-specific API support
- Online value reading requires Watch Table mechanism

<br/>

## 📝 Changelog

See [`CHANGELOG.md`](CHANGELOG.md) for version history.

<br/>

## 📜 License

This project is licensed under the terms in [`LICENSE.txt`](LICENSE.txt).

<br/>

---

<div align="center">

## 📬 Contact

**Hemang Joshi** — Founder, [hjLabs.in](https://hjlabs.in)

[![Email](https://img.shields.io/badge/Email-hemangjoshi37a@gmail.com-EA4335?style=for-the-badge&logo=gmail&logoColor=white)](mailto:hemangjoshi37a@gmail.com)
[![LinkedIn](https://img.shields.io/badge/LinkedIn-Hemang_Joshi-0A66C2?style=for-the-badge&logo=linkedin&logoColor=white)](https://www.linkedin.com/in/hemang-joshi-046746aa)
[![YouTube](https://img.shields.io/badge/YouTube-@HemangJoshi-FF0000?style=for-the-badge&logo=youtube&logoColor=white)](https://www.youtube.com/@HemangJoshi)
[![WhatsApp](https://img.shields.io/badge/WhatsApp-+91_7016525813-25D366?style=for-the-badge&logo=whatsapp&logoColor=white)](https://wa.me/917016525813)
[![Telegram](https://img.shields.io/badge/Telegram-@hjlabs-26A5E4?style=for-the-badge&logo=telegram&logoColor=white)](https://t.me/hjlabs)

<br/>

**hjLabs.in** — Industrial Automation | AI/ML | IoT | Algorithmic Trading

Serving **15+ countries** with a **4.9⭐ Google rating**

[![Website](https://img.shields.io/badge/🌐_hjLabs.in-Visit_Website-00A98F?style=for-the-badge)](https://hjlabs.in)
[![GitHub](https://img.shields.io/badge/GitHub-hemangjoshi37a-181717?style=for-the-badge&logo=github&logoColor=white)](https://github.com/hemangjoshi37a)
[![LinkTree](https://img.shields.io/badge/LinkTree-All_Links-39E09B?style=for-the-badge&logo=linktree&logoColor=white)](https://linktr.ee/hemangjoshi37a)

<br/>

---

<sub>Built with ❤️ by <a href="https://hjlabs.in">hjLabs.in</a> — Empowering industrial automation with AI</sub>

<br/>

⭐ **If this project helps you, please give it a star!** ⭐

</div>
