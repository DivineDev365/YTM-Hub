# YTM Hub 🎵

A lightweight, and completely portable desktop client for **YouTube Music** built natively for Windows using **WinUI 3** and the **Windows App SDK**.

![YTM Hub](Assets/StoreLogo.png)

YTM Hub takes the web experience of YouTube Music and wraps it into a premium Windows desktop application. But it doesn't stop there—YTM Hub features a **built-in local web server** that allows you to seamlessly control your music playback from your smartphone or any other device on your local network!

## ✨ Features

- **Modern WinUI 3 Interface**: Glassmorphism, Mica backdrops, and native Windows 11 styling.
- **Remote Control Web App**: Control Play/Pause, Next, Previous, and Volume directly from your phone.
- **QR Code Pairing**: A built-in flyout generates a QR code so you can instantly connect your phone's browser to the desktop app.
- **Portable & Standalone**: Fully compiled as a single `.NET` executable. No installation required.
- **Zero-Pollution**: Uses isolated `LocalAppData` for WebView2 storage to ensure your portable folder stays clean.

## 🚀 Getting Started

### Pre-requisites
To run or build this project, ensure you have:
- Windows 10 (version 1809 or later) or Windows 11.
- [.NET 10.0 SDK](https://dotnet.microsoft.com/) (if building from source).

### Portable Setup (No Installation)
1. Head over to the [Releases](../../releases) page and download the latest `YTMHub-Portable.zip`.
2. Extract the folder anywhere on your computer (e.g., your Desktop or a USB drive).
3. Double-click `YTMHub.exe` and sign in to your YouTube Music account!

## 📱 Using the Web Remote

Want to control your music from the couch?
1. Open **YTM Hub** on your PC.
2. Click the **Remote Server Toggle** in the top-right corner of the title bar and make sure that the local server toggle is turned on.
3. A QR code will drop down. Scan it with your phone's camera!
4. Your phone's web browser will open a sleek web remote. You can now control the volume, skip tracks, and pause playback wirelessly.

## 🛠️ Building from Source

To compile the portable executable yourself:

```bash
# Clone the repository
git clone https://github.com/Divine-Dev/YTMHub.git
cd YTMHub

# Publish as a single-file self-contained Windows executable
dotnet publish -c Release -r win-x64 --self-contained
```
The compiled portable application will be located in:
`bin\Release\net10.0-windows10.0.26100.0\win-x64\publish\`

## 📜 License

This project is licensed under the [MIT License](LICENSE) - see the LICENSE file for details.
