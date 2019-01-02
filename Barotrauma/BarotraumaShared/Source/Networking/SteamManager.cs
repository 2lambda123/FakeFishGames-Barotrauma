﻿using Barotrauma.Networking;
using Facepunch.Steamworks;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma.Steam
{
    class SteamManager
    {
#if DEBUG
        public static bool USE_STEAM
        {
            get { return GameMain.Config.UseSteam; }
        }
#else
        //cannot enable/disable steam in release builds
        public const bool USE_STEAM = true;
#endif

        public const uint AppID = 602960;
        
        private Facepunch.Steamworks.Client client;
        private Server server;

        private static SteamManager instance;
        public static SteamManager Instance
        {
            get
            {
                if (instance == null) instance = new SteamManager();
                return instance;
            }
        }
                        
        private bool isInitialized;
        public static bool IsInitialized
        {
            get
            {
                return Instance.isInitialized;
            }
        }
        
        public static void Initialize()
        {
            if (!USE_STEAM) return;
            instance = new SteamManager();
        }

        private SteamManager()
        {
            try
            {
                client = new Facepunch.Steamworks.Client(AppID);
                isInitialized = client.IsSubscribed && client.IsValid;

                if (isInitialized)
                {
                    DebugConsole.Log("Logged in as " + client.Username + " (SteamID " + client.SteamId + ")");
                }
            }
            catch (DllNotFoundException e)
            {
                isInitialized = false;
#if CLIENT
                new Barotrauma.GUIMessageBox(TextManager.Get("Error"), TextManager.Get("SteamDllNotFound"));
#else
                DebugConsole.ThrowError("Initializing Steam client failed (steam_api64.dll not found).", e);
#endif
            }
            catch (Exception e)
            {
                isInitialized = false;
#if CLIENT
                new Barotrauma.GUIMessageBox(TextManager.Get("Error"), TextManager.Get("SteamClientInitFailed"));
#else
                DebugConsole.ThrowError("Initializing Steam client failed.", e);
#endif
            }

            if (!isInitialized)
            {
                try
                {

                    Facepunch.Steamworks.Client.Instance.Dispose();
                }
                catch (Exception e)
                {
                    if (GameSettings.VerboseLogging) DebugConsole.ThrowError("Disposing Steam client failed.", e);
                }
            }
        }

        public static ulong GetSteamID()
        {
            if (instance == null || !instance.isInitialized)
            {
                return 0;
            }
            return instance.client.SteamId;
        }
        
        public static bool UnlockAchievement(string achievementName)
        {
            if (instance == null || !instance.isInitialized)
            {
                return false;
            }

            DebugConsole.Log("Unlocked achievement \"" + achievementName + "\"");
            return instance.client.Achievements.Trigger(achievementName);
        }

#region Connecting to servers

        public static bool GetServers(Action<Networking.ServerInfo> onServerFound, Action<Networking.ServerInfo> onServerRulesReceived, Action onFinished)
        {
            if (instance == null || !instance.isInitialized)
            {
                return false;
            }

            var filter = new ServerList.Filter
            {
                { "appid", AppID.ToString() },
                { "gamedir", "Barotrauma" },
                { "secure", "1" }
            };

            var query = instance.client.ServerList.Internet(filter);
            query.OnUpdate += () => { UpdateServerQuery(query, onServerFound, onServerRulesReceived); };
            query.OnFinished = onFinished;

            var localQuery = instance.client.ServerList.Local(filter);
            localQuery.OnUpdate += () => { UpdateServerQuery(localQuery, onServerFound, onServerRulesReceived); };
            localQuery.OnFinished = onFinished;

            return true;
        }

        private static void UpdateServerQuery(ServerList.Request query, Action<Networking.ServerInfo> onServerFound, Action<Networking.ServerInfo> onServerRulesReceived)
        {
            foreach (ServerList.Server s in query.Responded)
            {
                DebugConsole.Log(s.Name + " responded to server query.");
                var serverInfo = new Networking.ServerInfo()
                {
                    ServerName = s.Name,
                    Port = s.ConnectionPort.ToString(),
                    IP = s.Address.ToString(),
                    PlayerCount = s.Players,
                    MaxPlayers = s.MaxPlayers,
                    HasPassword = s.Passworded,
                };
                serverInfo.PingChecked = true;
                serverInfo.Ping = s.Ping;
                s.FetchRules();
                s.OnReceivedRules += (_) =>
                {
                    if (s.Rules.ContainsKey("message")) serverInfo.ServerMessage = s.Rules["message"];
                    if (s.Rules.ContainsKey("version")) serverInfo.GameVersion = s.Rules["version"];
                    if (s.Rules.ContainsKey("contentpackage")) serverInfo.ContentPackageNames.AddRange(s.Rules["contentpackage"].Split(','));
                    if (s.Rules.ContainsKey("contentpackagehash")) serverInfo.ContentPackageHashes.AddRange(s.Rules["contentpackagehash"].Split(','));
                    if (s.Rules.ContainsKey("usingwhitelist")) serverInfo.UsingWhiteList = s.Rules["usingwhitelist"]=="True";
                    if (s.Rules.ContainsKey("modeselectionmode"))
                    {
                        if (Enum.TryParse(s.Rules["modeselectionmode"], out SelectionMode selectionMode)) serverInfo.ModeSelectionMode = selectionMode;
                    }
                    if (s.Rules.ContainsKey("subselectionmode"))
                    {
                        if (Enum.TryParse(s.Rules["subselectionmode"], out SelectionMode selectionMode)) serverInfo.SubSelectionMode = selectionMode;
                    }
                    if (s.Rules.ContainsKey("allowspectating")) serverInfo.AllowSpectating = s.Rules["allowspectating"] == "True";
                    if (s.Rules.ContainsKey("allowrespawn")) serverInfo.AllowRespawn = s.Rules["allowrespawn"] == "True";
                    if (s.Rules.ContainsKey("traitors"))
                    {
                        if (Enum.TryParse(s.Rules["traitors"], out YesNoMaybe traitorsEnabled)) serverInfo.TraitorsEnabled = traitorsEnabled;
                    }

                    if (serverInfo.ContentPackageNames.Count != serverInfo.ContentPackageHashes.Count)
                    {
                        //invalid contentpackage info
                        serverInfo.ContentPackageNames.Clear();
                        serverInfo.ContentPackageHashes.Clear();
                    }
                    onServerRulesReceived(serverInfo);
                };

                onServerFound(serverInfo);
            }
            foreach (ServerList.Server s in query.Unresponsive)
            {
                DebugConsole.Log(s.Name + " did not respond to server query.");
            }
            query.Responded.Clear();
        }

        public static Auth.Ticket GetAuthSessionTicket()
        {
            if (instance == null || !instance.isInitialized)
            {
                return null;
            }

            return instance.client.Auth.GetAuthSessionTicket();
        }

#endregion

#region Server

        public static bool CreateServer(Networking.GameServer server)
        {
            if (instance == null || !instance.isInitialized)
            {
                return false;
            }

            ServerInit options = new ServerInit("Barotrauma", "Barotrauma")
            {
                GamePort = (ushort)server.Port,
                QueryPort = (ushort)server.QueryPort
            };
            //options.QueryShareGamePort();

            instance.server = new Server(AppID, options);
            if (!instance.server.IsValid)
            {
                instance.server.Dispose();
                instance.server = null;
                DebugConsole.ThrowError("Initializing Steam server failed.");
                return false;
            }

            RefreshServerDetails(server);

            instance.server.Auth.OnAuthChange = server.OnAuthChange;

            return true;
        }

        public static bool RegisterToMasterServer()
        {
            if (instance == null || !instance.isInitialized || instance.server == null) return false;
            Instance.server.LogOnAnonymous();
            return true;
        }

        public static bool RefreshServerDetails(Networking.GameServer server)
        {
            if (instance == null || !instance.isInitialized)
            {
                return false;
            }

            // These server state variables may be changed at any time.  Note that there is no lnoger a mechanism
            // to send the player count.  The player count is maintained by steam and you should use the player
            // creation/authentication functions to maintain your player count.
            instance.server.ServerName = server.Name;
            instance.server.MaxPlayers = server.MaxPlayers;
            instance.server.Passworded = server.HasPassword;
            Instance.server.SetKey("message", GameMain.NetLobbyScreen.ServerMessageText);
            Instance.server.SetKey("version", GameMain.Version.ToString());
            Instance.server.SetKey("contentpackage", string.Join(",", GameMain.Config.SelectedContentPackages.Select(cp => cp.Name)));
            Instance.server.SetKey("contentpackagehash", string.Join(",", GameMain.Config.SelectedContentPackages.Select(cp => cp.MD5hash.Hash)));
            Instance.server.SetKey("usingwhitelist", (server.WhiteList != null && server.WhiteList.Enabled).ToString());
            Instance.server.SetKey("modeselectionmode", server.ModeSelectionMode.ToString());
            Instance.server.SetKey("subselectionmode", server.SubSelectionMode.ToString());
            Instance.server.SetKey("allowspectating", server.AllowSpectating.ToString());
            Instance.server.SetKey("allowrespawn", server.AllowRespawn.ToString());
            Instance.server.SetKey("traitors", server.TraitorsEnabled.ToString());
            Instance.server.SetKey("gamestarted", server.GameStarted.ToString());
            Instance.server.SetKey("gamemode", server.GameMode);

#if SERVER
            instance.server.DedicatedServer = true;
#endif

            return true;
        }

        public static bool StartAuthSession(byte[] authTicketData, ulong clientSteamID)
        {
            if (instance == null || !instance.isInitialized || instance.server == null) return false;

            DebugConsole.Log("SteamManager authenticating Steam client " + clientSteamID);
            if (!instance.server.Auth.StartSession(authTicketData, clientSteamID))
            {
                DebugConsole.Log("Authentication failed");
                return false;
            }
            return true;
        }

        public static bool CloseServer()
        {
            if (instance == null || !instance.isInitialized || instance.server == null) return false;
            
            instance.server.Dispose();
            instance.server = null;
            
            return true;
        }

        #endregion

        #region Workshop

        public const string WorkshopItemStagingFolder = "NewWorkshopItem";
        public const string WorkshopItemPreviewImageFolder = "Workshop";
        public const string PreviewImageName = "PreviewImage.png";
        private const string MetadataFileName = "metadata.xml";
        private const string DefaultPreviewImagePath = "Content/DefaultWorkshopPreviewImage.png";

        private Sprite defaultPreviewImage;
        public Sprite DefaultPreviewImage
        {
            get
            {
                if (defaultPreviewImage == null)
                {
                    defaultPreviewImage = new Sprite(DefaultPreviewImagePath, sourceRectangle: null);
                }
                return defaultPreviewImage;
            }
        }

        public static void GetSubscribedWorkshopItems(Action<IList<Workshop.Item>> onItemsFound, List<string> requireTags = null)
        {
            if (instance == null || !instance.isInitialized) return;

            var query = instance.client.Workshop.CreateQuery();
            query.Order = Workshop.Order.RankedByTrend;
            query.UserId = instance.client.SteamId;
            query.UserQueryType = Workshop.UserQueryType.Subscribed;
            query.UploaderAppId = AppID;
            if (requireTags != null) query.RequireTags = requireTags;
            query.Run();
            query.OnResult += (Workshop.Query q) =>
            {
                onItemsFound?.Invoke(q.Items);
            };
        }

        public static void GetPopularWorkshopItems(Action<IList<Workshop.Item>> onItemsFound, int amount, List<string> requireTags = null)
        {
            if (instance == null || !instance.isInitialized) return;

            var query = instance.client.Workshop.CreateQuery();
            query.Order = Workshop.Order.RankedByTrend;
            query.RankedByTrendDays = 30;
            query.UploaderAppId = AppID;
            if (requireTags != null) query.RequireTags = requireTags;
            query.Run();
            query.OnResult += (Workshop.Query q) =>
            {
                var nonSubscribedItems = q.Items.Where(it => !it.Subscribed && !it.Installed);
                if (nonSubscribedItems.Count() > amount)
                {
                    nonSubscribedItems = nonSubscribedItems.Take(amount);
                }
                onItemsFound?.Invoke(nonSubscribedItems.ToList());
            };
        }

        public static void GetPublishedWorkshopItems(Action<IList<Workshop.Item>> onItemsFound, List<string> requireTags = null)
        {
            if (instance == null || !instance.isInitialized) return;

            var query = instance.client.Workshop.CreateQuery();
            query.Order = Workshop.Order.RankedByPublicationDate;
            query.UserId = instance.client.SteamId;
            query.UserQueryType = Workshop.UserQueryType.Published;
            query.UploaderAppId = AppID;
            if (requireTags != null) query.RequireTags = requireTags;
            query.Run();
            query.OnResult += (Workshop.Query q) =>
            {
                onItemsFound?.Invoke(q.Items);
            };
        }

        public static void SaveToWorkshop(Submarine sub)
        {
            if (instance == null || !instance.isInitialized) return;

            Workshop.Editor item;
            ContentPackage contentPackage;
            try
            {
                CreateWorkshopItemStaging(
                    new List<ContentFile>() { new ContentFile(sub.FilePath, ContentType.None) },
                    out item, out contentPackage);
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Creating the workshop item failed.", e);
                return;
            }

            item.Description = sub.Description;
            item.Title = sub.Name;
            item.Tags.Add("Submarine");

            string subPreviewPath = Path.GetFullPath(Path.Combine(item.Folder, PreviewImageName));
#if CLIENT
            try
            {
                using (Stream s = File.Create(subPreviewPath))
                {
                    sub.PreviewImage.Texture.SaveAsPng(s, (int)sub.PreviewImage.size.X, (int)sub.PreviewImage.size.Y);
                    item.PreviewImage = subPreviewPath;
                }
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Saving submarine preview image failed.", e);
                item.PreviewImage = null;
            }
#endif

            StartPublishItem(contentPackage, item);
        }

        /// <summary>
        /// Creates a new folder, copies the specified files there and creates a metadata file with install instructions.
        /// </summary>
        public static void CreateWorkshopItemStaging(List<ContentFile> contentFiles, out Workshop.Editor itemEditor, out ContentPackage contentPackage)
        {
            var stagingFolder = new DirectoryInfo(WorkshopItemStagingFolder);
            if (stagingFolder.Exists)
            {
                SaveUtil.ClearFolder(stagingFolder.FullName);
            }
            else
            {
                stagingFolder.Create();
            }
            Directory.CreateDirectory(Path.Combine(WorkshopItemStagingFolder, "Submarines"));
            Directory.CreateDirectory(Path.Combine(WorkshopItemStagingFolder, "Mods"));
            Directory.CreateDirectory(Path.Combine(WorkshopItemStagingFolder, "Mods", "ModName"));

            itemEditor = instance.client.Workshop.CreateItem(Workshop.ItemType.Community);
            itemEditor.Visibility = Workshop.Editor.VisibilityType.Public;
            itemEditor.WorkshopUploadAppId = AppID;
            itemEditor.Folder = stagingFolder.FullName;

            string previewImagePath = Path.GetFullPath(Path.Combine(itemEditor.Folder, PreviewImageName));
            File.Copy("Content/DefaultWorkshopPreviewImage.png", previewImagePath);

            //copy content files to the staging folder
            List<string> copiedFilePaths = new List<string>();
            foreach (ContentFile file in contentFiles)
            {
                string relativePath = UpdaterUtil.GetRelativePath(Path.GetFullPath(file.Path), Environment.CurrentDirectory);
                string destinationPath = Path.Combine(stagingFolder.FullName, relativePath);
                //make sure the directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                File.Copy(file.Path, destinationPath);
                copiedFilePaths.Add(destinationPath);
            }
            System.Diagnostics.Debug.Assert(copiedFilePaths.Count == contentFiles.Count);

            //create a new content package and include the copied files in it
            contentPackage = ContentPackage.CreatePackage("ContentPackage", Path.Combine(itemEditor.Folder, MetadataFileName), false);
            for (int i = 0; i < copiedFilePaths.Count; i++)
            {
                contentPackage.AddFile(copiedFilePaths[i], contentFiles[i].Type);
            }

            contentPackage.Save(Path.Combine(stagingFolder.FullName, MetadataFileName));
        }

        /// <summary>
        /// Creates a copy of the specified workshop item in the staging folder and an editor that can be used to edit and update the item
        /// </summary>
        public static void CreateWorkshopItemStaging(Workshop.Item existingItem, out Workshop.Editor itemEditor, out ContentPackage contentPackage)
        {
            if (!existingItem.Installed)
            {
                itemEditor = null;
                contentPackage = null;
                DebugConsole.ThrowError("Cannot edit the workshop item \"" + existingItem.Title + "\" because it has not been installed.");
                return;
            }

            var stagingFolder = new DirectoryInfo(WorkshopItemStagingFolder);
            if (stagingFolder.Exists)
            {
                SaveUtil.ClearFolder(stagingFolder.FullName);
            }
            else
            {
                stagingFolder.Create();
            }

            itemEditor = instance.client.Workshop.EditItem(existingItem.Id);
            itemEditor.Visibility = Workshop.Editor.VisibilityType.Public;
            itemEditor.Title = existingItem.Title;
            itemEditor.Tags = existingItem.Tags.ToList();
            itemEditor.Description = existingItem.Description;
            itemEditor.WorkshopUploadAppId = AppID;
            itemEditor.Folder = stagingFolder.FullName;

            string previewImagePath = Path.GetFullPath(Path.Combine(itemEditor.Folder, PreviewImageName));
            File.Copy("Content/DefaultWorkshopPreviewImage.png", previewImagePath);
            
            ContentPackage tempContentPackage = new ContentPackage(Path.Combine(existingItem.Directory.FullName, MetadataFileName));
            string newContentPackagePath = Path.Combine(WorkshopItemStagingFolder, MetadataFileName);
            File.Copy(tempContentPackage.Path, newContentPackagePath, overwrite: true);
            contentPackage = new ContentPackage(newContentPackagePath);

            foreach (ContentFile contentFile in tempContentPackage.Files)
            {
                string sourceFile = Path.Combine(existingItem.Directory.FullName, contentFile.Path);
                if (!File.Exists(sourceFile)) { continue; }
                //make sure the destination directory exists
                string destinationPath = Path.Combine(SteamManager.WorkshopItemStagingFolder, contentFile.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                File.Copy(sourceFile, destinationPath, overwrite: true);
                contentPackage.AddFile(contentFile.Path, contentFile.Type);
            }
        }

        public static void StartPublishItem(ContentPackage contentPackage, Workshop.Editor item)
        {
            if (instance == null || !instance.isInitialized) return;

            if (string.IsNullOrEmpty(item.Title))
            {
                DebugConsole.ThrowError("Cannot publish workshop item - title not set.");
                return;
            }
            if (string.IsNullOrEmpty(item.Folder))
            {
                DebugConsole.ThrowError("Cannot publish workshop item \"" + item.Title + "\" - folder not set.");
                return;
            }

            contentPackage.Name = item.Title;

            if (File.Exists(PreviewImageName)) File.Delete(PreviewImageName);
            //move the preview image out of the staging folder, it does not need to be included in the folder sent to Workshop
            File.Move(Path.GetFullPath(Path.Combine(item.Folder, PreviewImageName)), PreviewImageName);
            item.PreviewImage = Path.GetFullPath(PreviewImageName);

            CoroutineManager.StartCoroutine(PublishItem(item));
        }

        private static IEnumerable<object> PublishItem(Workshop.Editor item)
        {
            if (instance == null || !instance.isInitialized)
            {
                yield return CoroutineStatus.Success;
            }

            item.Publish();
            while (item.Publishing)
            {
                yield return CoroutineStatus.Running;
            }

            if (string.IsNullOrEmpty(item.Error))
            {
                DebugConsole.NewMessage("Published workshop item " + item.Title + " successfully.", Microsoft.Xna.Framework.Color.LightGreen);
            }
            else
            {
                DebugConsole.ThrowError("Publishing workshop item " + item.Title + " failed. " + item.Error);
            }
            
            SaveUtil.ClearFolder(WorkshopItemStagingFolder);
            Directory.Delete(WorkshopItemStagingFolder);
            File.Delete(PreviewImageName);

            yield return CoroutineStatus.Success;
        }

        /// <summary>
        /// Enables a workshop item by moving it to the game folder.
        /// </summary>
        public static bool EnableWorkShopItem(Workshop.Item item, bool allowFileOverwrite, out string errorMsg)
        {
            if (!item.Installed)
            {
                errorMsg = TextManager.Get("WorkshopErrorInstallRequiredToEnable").Replace("[itemname]", item.Title);
                DebugConsole.NewMessage(errorMsg, Microsoft.Xna.Framework.Color.Red);
                return false;
            }
            
            ContentPackage contentPackage = new ContentPackage(Path.Combine(item.Directory.FullName, MetadataFileName));
            string newContentPackagePath = GetWorkshopItemContentPackagePath(contentPackage);

            var allPackageFiles = Directory.GetFiles(item.Directory.FullName, "*", SearchOption.AllDirectories);
            List<string> nonContentFiles = new List<string>();
            foreach (string file in allPackageFiles)
            {
                if (file == MetadataFileName) { continue; }
                string relativePath = UpdaterUtil.GetRelativePath(file, item.Directory.FullName);
                string fullPath = Path.GetFullPath(relativePath);
                if (contentPackage.Files.Any(f => { string fp = Path.GetFullPath(f.Path); return fp == fullPath; })) { continue; }
                if (ContentPackage.IsModFilePathAllowed(relativePath))
                {
                    nonContentFiles.Add(relativePath);
                }
            }
                        
            if (!allowFileOverwrite)
            {
                if (File.Exists(newContentPackagePath))
                {
                    errorMsg = TextManager.Get("WorkshopErrorOverwriteOnEnable")
                        .Replace("[itemname]", item.Title)
                        .Replace("[filename]", newContentPackagePath);                        
                    DebugConsole.NewMessage(errorMsg, Microsoft.Xna.Framework.Color.Red);
                    return false;
                }

                foreach (ContentFile contentFile in contentPackage.Files)
                {
                    string sourceFile = Path.Combine(item.Directory.FullName, contentFile.Path);
                    if (File.Exists(sourceFile) && File.Exists(contentFile.Path))
                    {
                        errorMsg = TextManager.Get("WorkshopErrorOverwriteOnEnable")
                            .Replace("[itemname]", item.Title)
                            .Replace("[filename]", contentFile.Path);
                        DebugConsole.NewMessage(errorMsg, Microsoft.Xna.Framework.Color.Red);
                        return false;
                    }
                }
            }

            try
            {
                //we only need to create a new content package for the item if it contains content with a type other than None or Submarine
                //e.g. items that are just a sub file are just copied to the game folder
                if (contentPackage.Files.Any(f => f.Type != ContentType.None && f.Type != ContentType.Submarine))
                {
                    File.Copy(contentPackage.Path, newContentPackagePath);
                }

                foreach (ContentFile contentFile in contentPackage.Files)
                {
                    string sourceFile = Path.Combine(item.Directory.FullName, contentFile.Path);
                    if (!File.Exists(sourceFile)) { continue; }
                    if (!ContentPackage.IsModFilePathAllowed(contentFile))
                    {
                        DebugConsole.ThrowError(TextManager.Get("WorkshopErrorIllegalPathOnEnable").Replace("[filename]", contentFile.Path));
                        continue;
                    }
                    //make sure the destination directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(contentFile.Path));
                    File.Copy(sourceFile, contentFile.Path, overwrite: true);
                }

                foreach (string nonContentFile in nonContentFiles)
                {
                    string sourceFile = Path.Combine(item.Directory.FullName, nonContentFile);
                    if (!File.Exists(sourceFile)) { continue; }
                    if (!ContentPackage.IsModFilePathAllowed(nonContentFile))
                    {
                        DebugConsole.ThrowError(TextManager.Get("WorkshopErrorIllegalPathOnEnable").Replace("[filename]", nonContentFile));
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(nonContentFile));
                    File.Copy(sourceFile, nonContentFile, overwrite: true);
                }
            }
            catch (Exception e)
            {
                errorMsg = TextManager.Get("WorkshopErrorEnableFailed").Replace("[itemname]", item.Title) + " " + e.Message;
                DebugConsole.NewMessage(errorMsg, Microsoft.Xna.Framework.Color.Red);
                return false;
            }

            var newPackage = ContentPackage.CreatePackage(contentPackage.Name, newContentPackagePath, contentPackage.CorePackage);
            newPackage.SteamWorkshopUrl = item.Url;
            ContentPackage.List.Add(newPackage);
            if (newPackage.CorePackage)
            {
                //if enabling a core package, disable all other core packages
                GameMain.Config.SelectedContentPackages.RemoveWhere(cp => cp.CorePackage);
            }
            GameMain.Config.SelectedContentPackages.Add(newPackage);
            GameMain.Config.Save();

            if (item.Tags.Contains("Submarine") || newPackage.Files.Any(f => f.Type == ContentType.Submarine))
            {
                Submarine.RefreshSavedSubs();
            }

            errorMsg = "";
            return true;
        }

        /// <summary>
        /// Disables a workshop item by removing the files from the game folder.
        /// </summary>
        public static bool DisableWorkShopItem(Workshop.Item item, out string errorMsg)
        {
            if (!item.Installed)
            {
                errorMsg = "Cannot disable workshop item \"" + item.Title + "\" because it has not been installed.";
                DebugConsole.NewMessage(errorMsg, Microsoft.Xna.Framework.Color.Red);
                return false;
            }

            ContentPackage contentPackage = new ContentPackage(Path.Combine(item.Directory.FullName, MetadataFileName));
            string installedContentPackagePath = GetWorkshopItemContentPackagePath(contentPackage);

            var allPackageFiles = Directory.GetFiles(item.Directory.FullName, "*", SearchOption.AllDirectories);
            List<string> nonContentFiles = new List<string>();
            foreach (string file in allPackageFiles)
            {
                if (file == MetadataFileName) { continue; }
                string relativePath = UpdaterUtil.GetRelativePath(file, item.Directory.FullName);
                string fullPath = Path.GetFullPath(relativePath);
                if (contentPackage.Files.Any(f => { string fp = Path.GetFullPath(f.Path); return fp == fullPath; })) { continue; }
                if (ContentPackage.IsModFilePathAllowed(relativePath))
                {
                    nonContentFiles.Add(relativePath);
                }
            }
            if (File.Exists(installedContentPackagePath)) { File.Delete(installedContentPackagePath); }

            HashSet<string> directories = new HashSet<string>();
            try
            {
                foreach (ContentFile contentFile in contentPackage.Files)
                {
                    if (!ContentPackage.IsModFilePathAllowed(contentFile))
                    {
                        //Workshop items are not allowed to add or modify files in the Content or Data folders;
                        continue;
                    }
                    if (!File.Exists(contentFile.Path)) { continue; }
                    File.Delete(contentFile.Path);
                    directories.Add(Path.GetDirectoryName(contentFile.Path));
                }
                foreach (string nonContentFile in nonContentFiles)
                {
                    if (!ContentPackage.IsModFilePathAllowed(nonContentFile))
                    {
                        //Workshop items are not allowed to add or modify files in the Content or Data folders;
                        continue;
                    }
                    if (!File.Exists(nonContentFile)) { continue; }
                    File.Delete(nonContentFile);
                    directories.Add(Path.GetDirectoryName(nonContentFile));
                }
                
                foreach (string directory in directories)
                {
                    if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) { continue; }
                    if (Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Count() == 0)
                    {
                        Directory.Delete(directory, recursive: true);
                    }
                }

                ContentPackage.List.RemoveAll(cp => System.IO.Path.GetFullPath(cp.Path) == System.IO.Path.GetFullPath(installedContentPackagePath));
                GameMain.Config.SelectedContentPackages.RemoveWhere(cp => !ContentPackage.List.Contains(cp));
                GameMain.Config.Save();
            }
            catch (Exception e)
            {
                errorMsg = "Disabling the workshop item \"" + item.Title + "\" failed. "+e.Message;
                DebugConsole.NewMessage(errorMsg, Microsoft.Xna.Framework.Color.Red);
                return false;
            }
            errorMsg = "";
            return true;
        }

        public static bool CheckWorkshopItemEnabled(Workshop.Item item)
        {
            if (!item.Installed) return false;

            string metaDataPath = Path.Combine(item.Directory.FullName, MetadataFileName);
            if (!File.Exists(metaDataPath))
            {
                throw new FileNotFoundException("Metadata file for the Workshop item \"" + item.Title + "\" not found. The file may be corrupted.");
            }

            ContentPackage contentPackage = new ContentPackage(metaDataPath);
            //make sure the contentpackage file is present 
            //(unless the package only contains submarine files, in which case we don't need a content package)
            if (contentPackage.Files.Any(f => f.Type != ContentType.Submarine) &&
                !File.Exists(GetWorkshopItemContentPackagePath(contentPackage)))
            {
                return false;
            }
            foreach (ContentFile contentFile in contentPackage.Files)
            {
                if (!File.Exists(contentFile.Path)) return false;
            }
            
            return true;
        }

        public static string GetWorkshopItemContentPackagePath(ContentPackage contentPackage)
        {
            string fileName = contentPackage.Name + ".xml";
            string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            foreach (char c in invalidChars) fileName = fileName.Replace(c.ToString(), "");
            return Path.Combine("Data", "ContentPackages", fileName);
        }

        #endregion

        public static void Update(float deltaTime)
        {
            if (instance == null || !instance.isInitialized) return;
            
            instance.client?.Update();
            instance.server?.Update();

            SteamAchievementManager.Update(deltaTime);
        }

        public static void ShutDown()
        {
            if (instance == null || !instance.isInitialized)
            {
                return;
            }

            instance.client?.Dispose();
            instance.client = null;
            instance.server?.Dispose();
            instance.server = null;
        }
    }
}
