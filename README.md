# Canary Launcher Update
* C# WPF
* .NET 6.0

### Information

* ✅ Launcher like Tibia Global
* ✅ Download client
* ✅ Auto check update
* ✅ Update client
* ✅ Run the client

You must configure the "launcher_config.json" url in MainWindow.cs and SplashScreen.cs

In launcher_config.json you need to make necessary settings to use the launcher. (Read the explanation of how to use each configuration)

New configuration option:

* `clientPriority` - sets the priority class used when launching `client.exe`. Accepted values are the names from `ProcessPriorityClass` (for example `High` or `RealTime`).
