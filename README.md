# Albion Online C# Trading Bot (Photino + React Stack)

A professional, high-performance, low-resource automation and market-sniffing tool for **Albion Online**, built using a decoupled **C# (.NET 8) Backend** and a lightweight **React (Vite) Web UI** hosted via **Photino**.

---

## 🏗️ System Architecture

The application is structured into two main layers connected by an ultra-fast IPC bridge:
1. **The Native Backend (C#):** Handles background UDP packet sniffing (`SharpPcap`), Photon protocol reassembly, game state synchronization, state machine logic (FSM), and humanized mouse/keyboard automation via Win32 API.
2. **The Web UI (React & TailwindCSS):** A modern, resource-efficient dashboard running inside a native web view window to monitor silver balances, player positions, and real-time market order buffers.

---

## 📂 Project Directory Structure

```text
AlbionBot_Photino/
│
├── src-ui/                         # Vite + React Frontend Workspace
│   ├── package.json
│   ├── vite.config.js              # Configured to output bundle to /src-backend/wwwroot
│   ├── index.html
│   └── src/
│       ├── components/             # Reusable UI elements (Buttons, Modals, Status)
│       ├── pages/                  # Dashboard, Presets, Settings, Live Logs
│       └── App.jsx                 # Main React Router & IPC Web Message Listener
│
├── src-backend/                    # C# .NET 8 Core Backend
│   ├── AlbionBot.csproj            # Photino.NET & SharpPcap dependencies
│   ├── Program.cs                  # Bootstraps Photino window & loads wwwroot/index.html
│   ├── Network/                    # SharpPcap UDP Sniffer & Photon Package Parser
│   ├── GameState/                  # Thread-safe Player, Inventory, & Market singletons
│   ├── Automation/                 # Win32 SendInput, Mouse curves & OCR helpers
│   ├── Logic/                      # Bot Finite State Machine (FSM)
│   └── wwwroot/                    # Compiled static assets from the React build
│
└── Configs/                        # Static JSON Configurations
    ├── items_db.json
    └── mouse_positions.json
```

---

## ⚡ Technical Stack Rationale
- Photino.NET: Chosen over WPF/Blazor Hybrid or Electron because it completely eliminates heavy graphic framework runtimes (like WPF) and bloated browser runtimes (like Electron). It hooks directly into the native OS webview control, maximizing execution speed and minimizing RAM footprint.

- SharpPcap & PacketDotNet: Directly captures UDP ports 5055 and 5056 where Albion Online communicates via the Photon network protocol.

- React + Vite: Provides a reactive, snappy interface for displaying market data tables and graphs without lagging the background trading loop.

---

## 🌉 The IPC Bridge (C# <-> React Communication)
Communication between the C# backend and the React UI takes place via Photino's Native Web Message Handler:

### Frontend to Backend (Actions):

```JavaScript
// Triggered from React frontend
const sendCommand = (action, payload) => {
    window.external.sendMessage(JSON.stringify({ action, data: payload }));
};
```

### Backend to Frontend (Live Push Data):

```C#
// Pushed from C# backend sniffer to React UI
string payload = JsonSerializer.Serialize(new { type = "MARKET_UPDATE", prices = marketData });
window.SendWebMessage(payload);
```

---

## 🚀 Implementation Roadmap
1. Phase 1 (Skeleton Setup): Initialize the C# console project with Photino.NET and set up the Vite React workspace to build directly into wwwroot.

2. Phase 2 (Sniffer Porting): Port the photon parsing logic and UDP packet sniffer using SharpPcap, mapping event codes securely with thread-safe locks.

3. Phase 3 (UI Integration): Build out the Tailwind dashboard and wire up the IPC event listeners for live silver tracking and market data visualization.

4. Phase 4 (Automation & FSM): Implement the finite state machine logic coupled with Win32 mouse/keyboard inputs to execute automated trading routines safely.