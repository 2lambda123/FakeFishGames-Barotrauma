﻿using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Barotrauma.Networking
{
    partial class WhiteListedPlayer
    {
        private static UInt16 LastIdentifier = 0;

        public WhiteListedPlayer(string name,string ip)
        {
            Name = name;
            IP = ip;

            UniqueIdentifier = LastIdentifier; LastIdentifier++;
        }
    }

    partial class WhiteList
    {
        partial void InitProjSpecific()
        {
            if (File.Exists(SavePath))
            {
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(SavePath);
                }
                catch (Exception e)
                {
                    DebugConsole.ThrowError("Failed to open whitelist in " + SavePath, e);
                    return;
                }

                foreach (string line in lines)
                {
                    if (line[0] == '#')
                    {
                        string lineval = line.Substring(1, line.Length - 1);
                        int intVal = 0;
                        Int32.TryParse(lineval, out intVal);
                        if (lineval.ToLower() == "true" || intVal != 0)
                        {
                            Enabled = true;
                        }
                        else
                        {
                            Enabled = false;
                        }
                    }
                    else
                    {
                        string[] separatedLine = line.Split(',');
                        if (separatedLine.Length < 2) continue;

                        string name = String.Join(",", separatedLine.Take(separatedLine.Length - 1));
                        string ip = separatedLine.Last();

                        whitelistedPlayers.Add(new WhiteListedPlayer(name, ip));
                    }
                }
            }
        }

        public void Save()
        {
            GameServer.Log("Saving whitelist", ServerLog.MessageType.ServerMessage);

            List<string> lines = new List<string>();

            if (Enabled)
            {
                lines.Add("#true");
            }
            else
            {
                lines.Add("#false");
            }
            foreach (WhiteListedPlayer wlp in whitelistedPlayers)
            {
                lines.Add(wlp.Name + "," + wlp.IP);
            }

            try
            {
                File.WriteAllLines(SavePath, lines);
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Saving the whitelist to " + SavePath + " failed", e);
            }
        }

        public bool IsWhiteListed(string name, string ip)
        {
            if (!Enabled) return true;
            WhiteListedPlayer wlp = whitelistedPlayers.Find(p => p.Name == name);
            if (wlp == null) return false;
            if (wlp.IP != ip && !string.IsNullOrWhiteSpace(wlp.IP)) return false;
            return true;
        }

        private void RemoveFromWhiteList(WhiteListedPlayer wlp)
        {
            GameServer.Log("Removing " + wlp.Name + " from whitelist", ServerLog.MessageType.ServerMessage);

            whitelistedPlayers.Remove(wlp);
            Save();
        }

        private void AddToWhiteList(string name, string ip)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (whitelistedPlayers.Any(x => x.Name.ToLower() == name.ToLower() && x.IP == ip)) return;
            whitelistedPlayers.Add(new WhiteListedPlayer(name, ip));
            Save();
        }

        public void ServerAdminWrite(NetBuffer outMsg, Client c)
        {
            if (!c.HasPermission(ClientPermissions.ManageSettings))
            {
                outMsg.Write(false); outMsg.WritePadBits();
                return;
            }
            outMsg.Write(true);
            outMsg.Write(c.Connection == GameMain.Server.OwnerConnection);
            outMsg.Write(Enabled);

            outMsg.WritePadBits();
            outMsg.WriteVariableInt32(whitelistedPlayers.Count);
            for (int i = 0; i < whitelistedPlayers.Count; i++)
            {
                WhiteListedPlayer whitelistedPlayer = whitelistedPlayers[i];

                outMsg.Write(whitelistedPlayer.Name);
                outMsg.Write(whitelistedPlayer.UniqueIdentifier);
                if (c.Connection == GameMain.Server.OwnerConnection)
                {
                    outMsg.Write(whitelistedPlayer.IP);
                    //outMsg.Write(whitelistedPlayer.SteamID); //TODO: add steamid to whitelisted players
                }
            }
        }

        public bool ServerAdminRead(NetBuffer incMsg, Client c)
        {
            if (!c.HasPermission(ClientPermissions.ManageSettings))
            {
                bool enabled = incMsg.ReadBoolean(); incMsg.ReadPadBits();
                UInt16 removeCount = incMsg.ReadUInt16();
                incMsg.Position += removeCount * 4 * 8;
                return false;
            }
            else
            {
                bool enabled = incMsg.ReadBoolean(); incMsg.ReadPadBits();
                Enabled = enabled;

                UInt16 removeCount = incMsg.ReadUInt16();
                for (int i = 0; i < removeCount; i++)
                {
                    UInt16 id = incMsg.ReadUInt16();
                    WhiteListedPlayer whitelistedPlayer = whitelistedPlayers.Find(p => p.UniqueIdentifier == id);
                    if (whitelistedPlayer != null)
                    {
                        GameServer.Log(c.Name + " removed " + whitelistedPlayer.Name + " from whitelist (" + whitelistedPlayer.IP + ")", ServerLog.MessageType.ConsoleUsage);
                        RemoveFromWhiteList(whitelistedPlayer);
                    }
                }

                UInt16 addCount = incMsg.ReadUInt16();
                for  (int i = 0; i < addCount; i++)
                {
                    string name = incMsg.ReadString();
                    string ip = incMsg.ReadString();
                }
                
                return removeCount > 0 || addCount > 0;
            }
        }
    }
}
