"# proxy-blocker" 
"# proxyblocker" 
"# proxyblocker" 
"# proxyblocker" 
# 🛡️ Domain Filter Script (C#)

This project is a Windows service written in **C#** that blocks access to websites listed in one or more `.txt` files. It acts as a basic content filtering mechanism by monitoring network requests and preventing access to specified domains.

## 📌 Features

- ✅ Blocks access to domains listed in one or more `.txt` files
- 🧠 Works system-wide as a background service
- 📄 Logs activity to Windows Event Viewer for auditing and troubleshooting
- 💡 Designed to be extensible and lightweight
- 🔒 Helps prevent access to malicious, time-wasting, or non-compliant sites

## 🔧 How It Works

The script uses a system-wide proxy that inspects outbound web traffic. If a request matches any domain in the blacklist (from the `.txt` file), it is blocked or dropped silently.

### Block Logic:
- Reads domain names from a `.txt` file (one domain per line)
- Monitors outgoing requests
- If the requested domain matches an entry, access is denied

## 📁 Folder Structure

DomainFilter/
├── DomainFilterService.cs # Main Windows Service logic
├── DomainBlocker.cs # Logic to intercept and filter requests
├── fakenews.txt, gambling.txt, malware.txt, nsfw.txt, socialmedia.txt # List of blocked domains
├── Logger.cs # Event viewer logging
├── DomainFilterServiceInstaller.cs # Windows service installer config
└── README.md
Build the project in Visual Studio as a Windows Service.

1. Install the service (Run as Administrator):

sc create DomainFilterService binPath= "C:\Path\To\Your\Executable.exe"
2. Start the service:
net start DomainFilterService
Check Event Viewer for logs under Applications and Services Logs > DomainFilter
