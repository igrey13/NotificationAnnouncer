# NotificationAnnouncer

An Android MAUI app that listens to notifications from subscribed apps, captures and dismisses them, queues the messages, and reads them aloud using KittenTTS — all while running in the background.

## Features
- 🔔 Notification Listener — captures & dismisses notifications from subscribed apps
- 📬 Message Queue — stores captured messages in a FIFO queue
- 🗣️ KittenTTS TTS — reads messages aloud one at a time
- ⚙️ Background Foreground Service — keeps the app running in the background

## Setup
1. Clone the repo
2. Open in Visual Studio 2022+
3. Build and deploy to an Android device (API 26+)
4. Grant Notification Access via `Settings → Apps → Special App Access → Notification Access`
