﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Barotrauma.Networking
{
    [Flags]
    enum ClientPermissions
    {
        None = 0x0,
        ManageRound = 0x1,
        Kick = 0x2,
        Ban = 0x4,
        Unban = 0x8,
        SelectSub = 0x10,
        SelectMode = 0x20,
        ManageCampaign = 0x40,
        ConsoleCommands = 0x80,
        ServerLog = 0x100,
        ManageSettings = 0x200,
        ManagePermissions = 0x400,
        All = 0x7ff
    }

    class PermissionPreset
    {
        public static List<PermissionPreset> List = new List<PermissionPreset>();
           
        public readonly string Name;
        public readonly string Description;
        public readonly ClientPermissions Permissions;
        public readonly List<DebugConsole.Command> PermittedCommands;
        
        public PermissionPreset(XElement element)
        {
            Name = element.GetAttributeString("name", "");
            Description = element.GetAttributeString("description", "");

            string permissionsStr = element.GetAttributeString("permissions", "");
            if (!Enum.TryParse(permissionsStr, out Permissions))
            {
                DebugConsole.ThrowError("Error in permission preset \"" + Name + "\" - " + permissionsStr + " is not a valid permission!");
            }

            PermittedCommands = new List<DebugConsole.Command>();
            if (Permissions.HasFlag(ClientPermissions.ConsoleCommands))
            {
                foreach (XElement subElement in element.Elements())
                {
                    if (subElement.Name.ToString().ToLowerInvariant() != "command") continue;
                    string commandName = subElement.GetAttributeString("name", "");

                    DebugConsole.Command command = DebugConsole.FindCommand(commandName);
                    if (command == null)
                    {
                        DebugConsole.ThrowError("Error in permission preset \"" + Name + "\" - " + commandName + "\" is not a valid console command.");
                        continue;
                    }

                    PermittedCommands.Add(command);
                }
            }
        }

        public static void LoadAll(string file)
        {
            if (!File.Exists(file)) return;

            XDocument doc = XMLExtensions.TryLoadXml(file);
            if (doc == null || doc.Root == null) return;

            foreach (XElement element in doc.Root.Elements())
            {
                List.Add(new PermissionPreset(element));
            }
        }
    }
}
