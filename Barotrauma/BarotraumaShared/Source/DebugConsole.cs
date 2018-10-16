﻿using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Barotrauma.Steam;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

namespace Barotrauma
{
    struct ColoredText
    {
        public string Text;
        public Color Color;
		public bool IsCommand;

        public readonly string Time;

        public ColoredText(string text, Color color, bool isCommand)
        {
            this.Text = text;
            this.Color = color;
			this.IsCommand = isCommand;

            Time = DateTime.Now.ToString();
        }
    }

    static partial class DebugConsole
    {
        public partial class Command
        {
            public readonly string[] names;
            public readonly string help;
            
            public Action<string[]> OnExecute;

            public Func<string[][]> GetValidArgs;

            /// <summary>
            /// Using a command that's considered a cheat disables achievements
            /// </summary>
            public readonly bool IsCheat;

            /// <summary>
            /// Use this constructor to create a command that executes the same action regardless of whether it's executed by a client or the server.
            /// </summary>
            public Command(string name, string help, Action<string[]> onExecute, Func<string[][]> getValidArgs = null, bool isCheat = false)
            {
                names = name.Split('|');
                this.help = help;

                this.OnExecute = onExecute;
                
                this.GetValidArgs = getValidArgs;
                this.IsCheat = isCheat;
            }

            public void Execute(string[] args)
            {
                if (OnExecute == null) return;
                if (!CheatsEnabled && IsCheat)
                {
                    NewMessage("You need to enable cheats using the command \"enablecheats\" before you can use the command \"" + names[0] + "\".", Color.Red);
                    if (GameMain.Config.UseSteam)
                    {
                        NewMessage("Enabling cheats will disable Steam achievements during this play session.", Color.Red);
                    }
                    return;
                }

                OnExecute(args);
            }
        }
        
        static partial void AddHelpMessage(Command command);
        
        const int MaxMessages = 300;

        public static List<ColoredText> Messages = new List<ColoredText>();

        public delegate void QuestionCallback(string answer);
        private static QuestionCallback activeQuestionCallback;

        private static List<Command> commands = new List<Command>();
        public static List<Command> Commands
        {
            get { return commands; }
        }
        
        private static string currentAutoCompletedCommand;
        private static int currentAutoCompletedIndex;

        //used for keeping track of the message entered when pressing up/down
        static int selectedIndex;

        public static bool CheatsEnabled;

        private static List<ColoredText> unsavedMessages = new List<ColoredText>();
        private static int messagesPerFile = 800;
        public const string SavePath = "ConsoleLogs";

        private static void AssignOnExecute(string names, Action<string[]> onExecute)
        {
            commands.First(c => c.names.Intersect(names.Split('|')).Count() > 0).OnExecute = onExecute;
        }

        static DebugConsole()
        {
#if DEBUG
            CheatsEnabled = true;
#endif

            commands.Add(new Command("help", "", (string[] args) =>
            {
                if (args.Length == 0)
                {
                    foreach (Command c in commands)
                    {
                        if (string.IsNullOrEmpty(c.help)) continue;
#if CLIENT
                        AddHelpMessage(c);
#else
                        NewMessage(c.help, Color.Cyan);
#endif
                    }
                }
                else
                {
                    var matchingCommand = commands.Find(c => c.names.Any(name => name == args[0]));
                    if (matchingCommand == null)
                    {
                        NewMessage("Command " + args[0] + " not found.", Color.Red);
                    }
                    else
                    {
                        AddHelpMessage(matchingCommand);
                    }
                }
            }, 
            () =>
            {
                return new string[][]
                {
                    commands.SelectMany(c => c.names).ToArray(),
                    new string[0]
                };
            }));


            commands.Add(new Command("items|itemlist", "itemlist: List all the item prefabs available for spawning.", (string[] args) =>
            {
                NewMessage("***************", Color.Cyan);
                foreach (MapEntityPrefab ep in MapEntityPrefab.List)
                {
                    var itemPrefab = ep as ItemPrefab;
                    if (itemPrefab == null || itemPrefab.Name == null) continue;
                    string text = $"- {itemPrefab.Name}";
                    if (itemPrefab.Tags.Any())
                    {
                        text += $" ({string.Join(", ", itemPrefab.Tags)})";
                    }
                    if (itemPrefab.AllowedLinks.Any())
                    {
                        text += $", Links: {string.Join(", ", itemPrefab.AllowedLinks)}";
                    }
                    NewMessage(text, Color.Cyan);
                }
                NewMessage("***************", Color.Cyan);
            }));
            
            commands.Add(new Command("createfilelist", "", (string[] args) =>
            {
                UpdaterUtil.SaveFileList("filelist.xml");
            }));

            commands.Add(new Command("spawn|spawncharacter", "spawn [creaturename] [near/inside/outside/cursor]: Spawn a creature at a random spawnpoint (use the second parameter to only select spawnpoints near/inside/outside the submarine).", (string[] args) =>
            {
                SpawnCharacter(args, GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition), out string errorMsg);
                if (!string.IsNullOrWhiteSpace(errorMsg))
                {
                    ThrowError(errorMsg);
                }
            },
            () =>
            {
                List<string> characterFiles = GameMain.Instance.GetFilesOfType(ContentType.Character).ToList();
                for (int i = 0; i < characterFiles.Count; i++)
                {
                    characterFiles[i] = Path.GetFileNameWithoutExtension(characterFiles[i]).ToLowerInvariant();
                }

                return new string[][]
                {
                characterFiles.ToArray(),
                new string[] { "near", "inside", "outside", "cursor" }
                };
            }, isCheat: true));

            commands.Add(new Command("spawnitem", "spawnitem [itemname] [cursor/inventory/cargo/random/[name]]: Spawn an item at the position of the cursor, in the inventory of the controlled character, in the inventory of the client with the given name, or at a random spawnpoint if the last parameter is omitted or \"random\".",
            (string[] args) =>
            {
                SpawnItem(args, GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition), Character.Controlled, out string errorMsg);
                if (!string.IsNullOrWhiteSpace(errorMsg))
                {
                    ThrowError(errorMsg);
                }
            },
            () =>
            {
                List<string> itemNames = new List<string>();
                foreach (MapEntityPrefab prefab in MapEntityPrefab.List)
                {
                    if (prefab is ItemPrefab itemPrefab) itemNames.Add(itemPrefab.Name);
                }

                List<string> spawnPosParams = new List<string>() { "cursor", "inventory" };
#if SERVER
                if (GameMain.Server != null) spawnPosParams.AddRange(GameMain.Server.ConnectedClients.Select(c => c.Name));
#endif
                spawnPosParams.AddRange(Character.CharacterList.Where(c => c.Inventory != null).Select(c => c.Name).Distinct());

                return new string[][]
                {
                itemNames.ToArray(),
                spawnPosParams.ToArray()
                };
            }, isCheat: true));


            commands.Add(new Command("disablecrewai", "disablecrewai: Disable the AI of the NPCs in the crew.", (string[] args) =>
            {
                HumanAIController.DisableCrewAI = true;
                NewMessage("Crew AI disabled", Color.White);
            }));

            commands.Add(new Command("enablecrewai", "enablecrewai: Enable the AI of the NPCs in the crew.", (string[] args) =>
            {
                HumanAIController.DisableCrewAI = false;
                NewMessage("Crew AI enabled", Color.White);
            }, isCheat: true));

            commands.Add(new Command("botcount", "botcount [x]: Set the number of bots in the crew in multiplayer.", null));

            commands.Add(new Command("botspawnmode", "botspawnmode [fill/normal]: Set how bots are spawned in the multiplayer.", null));

            commands.Add(new Command("autorestart", "autorestart [true/false]: Enable or disable round auto-restart.", null));

            commands.Add(new Command("autorestartinterval", "autorestartinterval [seconds]: Set how long the server waits between rounds before automatically starting a new one. If set to 0, autorestart is disabled.", null));

            commands.Add(new Command("autorestarttimer", "autorestarttimer [seconds]: Set the current autorestart countdown to the specified value.", null));

            commands.Add(new Command("giveperm", "giveperm [id]: Grants administrative permissions to the player with the specified client ID.", null));

            commands.Add(new Command("revokeperm", "revokeperm [id]: Revokes administrative permissions to the player with the specified client ID.", null));
            
            commands.Add(new Command("giverank", "giverank [id]: Assigns a specific rank (= a set of administrative permissions) to the player with the specified client ID.", null));

            commands.Add(new Command("givecommandperm", "givecommandperm [id]: Gives the player with the specified client ID the permission to use the specified console commands.", null));

            commands.Add(new Command("revokecommandperm", "revokecommandperm [id]: Revokes permission to use the specified console commands from the player with the specified client ID.", null));
            
            commands.Add(new Command("showperm", "showperm [id]: Shows the current administrative permissions of the client with the specified client ID.", null));

            commands.Add(new Command("togglekarma", "togglekarma: Toggles the karma system.", null));

            commands.Add(new Command("kick", "kick [name]: Kick a player out of the server.", (string[] args) =>
            {
                if (GameMain.NetworkMember == null || args.Length == 0) return;

                string playerName = string.Join(" ", args);

                ShowQuestionPrompt("Reason for kicking \"" + playerName + "\"?", (reason) =>
                {
                    GameMain.NetworkMember.KickPlayer(playerName, reason);
                });
            },
            () =>
            {
                if (GameMain.NetworkMember == null) return null;

                return new string[][]
                {
                    GameMain.NetworkMember.ConnectedClients.Select(c => c.Name).ToArray()
                };
            }));

            commands.Add(new Command("kickid", "kickid [id]: Kick the player with the specified client ID out of the server.", (string[] args) =>
            {
                if (GameMain.NetworkMember == null || args.Length == 0) return;

                int.TryParse(args[0], out int id);
                var client = GameMain.NetworkMember.ConnectedClients.Find(c => c.ID == id);
                if (client == null)
                {
                    ThrowError("Client id \"" + id + "\" not found.");
                    return;
                }

                ShowQuestionPrompt("Reason for kicking \"" + client.Name + "\"?", (reason) =>
                {
                    GameMain.NetworkMember.KickPlayer(client.Name, reason);
                });
            }));

            commands.Add(new Command("ban", "ban [name]: Kick and ban the player from the server.", (string[] args) =>
            {
                if (GameMain.NetworkMember == null || args.Length == 0) return;

                string clientName = string.Join(" ", args);
                ShowQuestionPrompt("Reason for banning \"" + clientName + "\"?", (reason) =>
                {
                    ShowQuestionPrompt("Enter the duration of the ban (leave empty to ban permanently, or use the format \"[days] d [hours] h\")", (duration) =>
                    {
                        TimeSpan? banDuration = null;
                        if (!string.IsNullOrWhiteSpace(duration))
                        {
                            if (!TryParseTimeSpan(duration, out TimeSpan parsedBanDuration))
                            {
                                ThrowError("\"" + duration + "\" is not a valid ban duration. Use the format \"[days] d [hours] h\", \"[days] d\" or \"[hours] h\".");
                                return;
                            }
                            banDuration = parsedBanDuration;
                        }

                        GameMain.NetworkMember.BanPlayer(clientName, reason, false, banDuration);
                    });
                });
            },
            () =>
            {
                if (GameMain.NetworkMember == null) return null;

                return new string[][]
                {
                    GameMain.NetworkMember.ConnectedClients.Select(c => c.Name).ToArray()
                };
            }));

            commands.Add(new Command("banid", "banid [id]: Kick and ban the player with the specified client ID from the server.", (string[] args) =>
            {
                if (GameMain.NetworkMember == null || args.Length == 0) return;

                int.TryParse(args[0], out int id);
                var client = GameMain.NetworkMember.ConnectedClients.Find(c => c.ID == id);
                if (client == null)
                {
                    ThrowError("Client id \"" + id + "\" not found.");
                    return;
                }

                ShowQuestionPrompt("Reason for banning \"" + client.Name + "\"?", (reason) =>
                {
                    ShowQuestionPrompt("Enter the duration of the ban (leave empty to ban permanently, or use the format \"[days] d [hours] h\")", (duration) =>
                    {
                        TimeSpan? banDuration = null;
                        if (!string.IsNullOrWhiteSpace(duration))
                        {
                            if (!TryParseTimeSpan(duration, out TimeSpan parsedBanDuration))
                            {
                                ThrowError("\"" + duration + "\" is not a valid ban duration. Use the format \"[days] d [hours] h\", \"[days] d\" or \"[hours] h\".");
                                return;
                            }
                            banDuration = parsedBanDuration;
                        }

                        GameMain.NetworkMember.BanPlayer(client.Name, reason, false, banDuration);
                    });
                });
            }));


            commands.Add(new Command("banip", "banip [ip]: Ban the IP address from the server.", null));

            commands.Add(new Command("teleportcharacter|teleport", "teleport [character name]: Teleport the specified character to the position of the cursor. If the name parameter is omitted, the controlled character will be teleported.", (string[] args) =>
            {
                Character tpCharacter = (args.Length == 0) ? Character.Controlled : FindMatchingCharacter(args, false);
                if (tpCharacter == null) return;

                var cam = GameMain.GameScreen.Cam;
                tpCharacter.AnimController.CurrentHull = null;
                tpCharacter.Submarine = null;
                tpCharacter.AnimController.SetPosition(ConvertUnits.ToSimUnits(cam.ScreenToWorld(PlayerInput.MousePosition)));
                tpCharacter.AnimController.FindHull(cam.ScreenToWorld(PlayerInput.MousePosition), true);
            },
            () =>
            {
                return new string[][]
                {
                    Character.CharacterList.Select(c => c.Name).Distinct().ToArray()
                };
            }, isCheat: true));

            commands.Add(new Command("godmode", "godmode: Toggle submarine godmode. Makes the main submarine invulnerable to damage.", (string[] args) =>
            {
                if (Submarine.MainSub == null) return;

                Submarine.MainSub.GodMode = !Submarine.MainSub.GodMode;
                NewMessage(Submarine.MainSub.GodMode ? "Godmode on" : "Godmode off", Color.White);
            }, isCheat: true));

            commands.Add(new Command("lock", "lock: Lock movement of the main submarine.", (string[] args) =>
            {
                Submarine.LockX = !Submarine.LockX;
                Submarine.LockY = Submarine.LockX;
                NewMessage((Submarine.LockX ? "Submarine movement locked." : "Submarine movement unlocked."), Color.White);
            }, null, true));

            commands.Add(new Command("lockx", "lockx: Lock horizontal movement of the main submarine.", (string[] args) =>
            {
                Submarine.LockX = !Submarine.LockX;
                NewMessage((Submarine.LockX ? "Horizontal submarine movement locked." : "Horizontal submarine movement unlocked."), Color.White);
            }, null, true));

            commands.Add(new Command("locky", "locky: Lock vertical movement of the main submarine.", (string[] args) =>
            {
                Submarine.LockY = !Submarine.LockY;
                NewMessage((Submarine.LockY ? "Vertical submarine movement locked." : "Vertical submarine movement unlocked."), Color.White);
            }, null, true));

            commands.Add(new Command("dumpids", "", (string[] args) =>
            {
                try
                {
                    int count = args.Length == 0 ? 10 : int.Parse(args[0]);
                    Entity.DumpIds(count);
                }
                catch (Exception e)
                {
                    ThrowError("Failed to dump ids", e);
                }
            }));

            commands.Add(new Command("findentityids", "findentityids [entityname]", (string[] args) =>
            {
                if (args.Length == 0) return;
                args[0] = args[0].ToLowerInvariant();
                foreach (MapEntity mapEntity in MapEntity.mapEntityList)
                {
                    if (mapEntity.Name.ToLowerInvariant() == args[0])
                    {
                        ThrowError(mapEntity.ID + ": " + mapEntity.Name.ToString());
                    }
                }
                foreach (Character character in Character.CharacterList)
                {
                    if (character.Name.ToLowerInvariant() == args[0] || character.SpeciesName.ToLowerInvariant() == args[0])
                    {
                        ThrowError(character.ID + ": " + character.Name.ToString());
                    }
                }
            }));

            commands.Add(new Command("giveaffliction", "giveaffliction [affliction name] [affliction strength] [character name]: Add an affliction to a character. If the name parameter is omitted, the affliction is added to the controlled character.", (string[] args) =>
            {
                if (args.Length < 2) return;

                AfflictionPrefab afflictionPrefab = AfflictionPrefab.List.Find(a =>
                    a.Name.ToLowerInvariant() == args[0].ToLowerInvariant() ||
                    a.Identifier.ToLowerInvariant() == args[0].ToLowerInvariant());
                if (afflictionPrefab == null)
                {
                    ThrowError("Affliction \"" + args[0] + "\" not found.");
                    return;
                }

                if (!float.TryParse(args[1], out float afflictionStrength))
                {
                    ThrowError("\"" + args[1] + "\" is not a valid affliction strength.");
                    return;
                }

                Character targetCharacter = (args.Length <= 2) ? Character.Controlled : FindMatchingCharacter(args.Skip(2).ToArray());
                if (targetCharacter != null)
                {
                    targetCharacter.CharacterHealth.ApplyAffliction(targetCharacter.AnimController.MainLimb, afflictionPrefab.Instantiate(afflictionStrength));
                }
            },
            () =>
            {
                return new string[][]
                {
                    AfflictionPrefab.List.Select(a => a.Name).ToArray(),
                    new string[] { "1" },
                    Character.CharacterList.Select(c => c.Name).ToArray()
                };
            }, isCheat: true));

            commands.Add(new Command("heal", "heal [character name]: Restore the specified character to full health. If the name parameter is omitted, the controlled character will be healed.", (string[] args) =>
            {
                Character healedCharacter = (args.Length == 0) ? Character.Controlled : FindMatchingCharacter(args);
                if (healedCharacter != null)
                {
                    healedCharacter.SetAllDamage(0.0f, 0.0f, 0.0f);
                    healedCharacter.Oxygen = 100.0f;
                    healedCharacter.Bloodloss = 0.0f;
                    healedCharacter.SetStun(0.0f, true);
                }
            },
            () =>
            {
                return new string[][]
                {
                    Character.CharacterList.Select(c => c.Name).Distinct().ToArray()
                };
            }, isCheat: true));

            commands.Add(new Command("revive", "revive [character name]: Bring the specified character back from the dead. If the name parameter is omitted, the controlled character will be revived.", (string[] args) =>
            {
                Character revivedCharacter = (args.Length == 0) ? Character.Controlled : FindMatchingCharacter(args);
                if (revivedCharacter == null) return;

                revivedCharacter.Revive();
#if SERVER
                if (GameMain.Server != null)
                {
                    foreach (Client c in GameMain.Server.ConnectedClients)
                    {
                        if (c.Character != revivedCharacter) continue;

                        //clients stop controlling the character when it dies, force control back
                        GameMain.Server.SetClientCharacter(c, revivedCharacter);
                        break;
                    }
                }
#endif
            },
            () =>
            {
                return new string[][]
                {
        Character.CharacterList.Select(c => c.Name).Distinct().ToArray()
                };
            }, isCheat: true));

            commands.Add(new Command("freeze", "", (string[] args) =>
            {
                if (Character.Controlled != null) Character.Controlled.AnimController.Frozen = !Character.Controlled.AnimController.Frozen;
            }, isCheat: true));

            commands.Add(new Command("ragdoll", "ragdoll [character name]: Force-ragdoll the specified character. If the name parameter is omitted, the controlled character will be ragdolled.", (string[] args) =>
            {
                Character ragdolledCharacter = (args.Length == 0) ? Character.Controlled : FindMatchingCharacter(args);
                if (ragdolledCharacter != null)
                {
                    ragdolledCharacter.IsForceRagdolled = !ragdolledCharacter.IsForceRagdolled;
                }
            },
            () =>
            {
                return new string[][]
                {
        Character.CharacterList.Select(c => c.Name).Distinct().ToArray()
                };
            }, isCheat: true));

            commands.Add(new Command("freecamera|freecam", "freecam: Detach the camera from the controlled character.", (string[] args) =>
            {
                Character.Controlled = null;
                GameMain.GameScreen.Cam.TargetPos = Vector2.Zero;
            }, isCheat: true));

            commands.Add(new Command("eventmanager", "eventmanager: Toggle event manager on/off. No new random events are created when the event manager is disabled.", (string[] args) =>
            {
                if (GameMain.GameSession?.EventManager != null)
                {
                    GameMain.GameSession.EventManager.Enabled = !GameMain.GameSession.EventManager.Enabled;
                    NewMessage(GameMain.GameSession.EventManager.Enabled ? "Event manager on" : "Event manager off", Color.White);
                }
            }, isCheat: true));

            commands.Add(new Command("water|editwater", "water/editwater: Toggle water editing. Allows adding water into rooms by holding the left mouse button and removing it by holding the right mouse button.", (string[] args) =>
            {
                if (GameMain.NetworkMember == null || !GameMain.NetworkMember.IsClient)
                {
                    Hull.EditWater = !Hull.EditWater;
                    NewMessage(Hull.EditWater ? "Water editing on" : "Water editing off", Color.White);
                }
            }, isCheat: true));

            commands.Add(new Command("fire|editfire", "fire/editfire: Allows putting up fires by left clicking.", (string[] args) =>
            {
                if (GameMain.NetworkMember == null || !GameMain.NetworkMember.IsClient)
                {
                    Hull.EditFire = !Hull.EditFire;
                    NewMessage(Hull.EditFire ? "Fire spawning on" : "Fire spawning off", Color.White);
                }
            }, isCheat: true));

            commands.Add(new Command("explosion", "explosion [range] [force] [damage] [structuredamage] [emp strength]: Creates an explosion at the position of the cursor.", (string[] args) =>
            {
                Vector2 explosionPos = GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition);
                float range = 500, force = 10, damage = 50, structureDamage = 10, empStrength = 0.0f;
                if (args.Length > 0) float.TryParse(args[0], out range);
                if (args.Length > 1) float.TryParse(args[1], out force);
                if (args.Length > 2) float.TryParse(args[2], out damage);
                if (args.Length > 3) float.TryParse(args[3], out structureDamage);
                if (args.Length > 4) float.TryParse(args[4], out empStrength);
                new Explosion(range, force, damage, structureDamage, empStrength).Explode(explosionPos, null);
            }, isCheat: true));

#if DEBUG
            commands.Add(new Command("waterparams", "waterparams [stiffness] [spread] [damping]: defaults 0.02, 0.05, 0.05", (string[] args) =>
            {
                Vector2 explosionPos = GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition);
                float stiffness = 0.02f, spread = 0.05f, damp = 0.01f;
                if (args.Length > 0) float.TryParse(args[0], out stiffness);
                if (args.Length > 1) float.TryParse(args[1], out spread);
                if (args.Length > 2) float.TryParse(args[2], out damp);
                Hull.WaveStiffness = stiffness;
                Hull.WaveSpread = spread;
                Hull.WaveDampening = damp;
            }, null));
#endif

            commands.Add(new Command("fixitems", "fixitems: Repairs all items and restores them to full condition.", (string[] args) =>
            {
                foreach (Item it in Item.ItemList)
                {
                    it.Condition = it.Prefab.Health;
                }
            }, null, true));

            commands.Add(new Command("fixhulls|fixwalls", "fixwalls/fixhulls: Fixes all walls.", (string[] args) =>
            {
                foreach (Structure w in Structure.WallList)
                {
                    for (int i = 0; i < w.SectionCount; i++)
                    {
                        w.AddDamage(i, -100000.0f);
                    }
                }
            }, null, true));

            commands.Add(new Command("power", "power [temperature]: Immediately sets the temperature of the nuclear reactor to the specified value.", (string[] args) =>
            {
                Item reactorItem = Item.ItemList.Find(i => i.GetComponent<Reactor>() != null);
                if (reactorItem == null) return;

                float power = 1000.0f;
                if (args.Length > 0) float.TryParse(args[0], out power);

                var reactor = reactorItem.GetComponent<Reactor>();
                reactor.TurbineOutput = power / reactor.MaxPowerOutput * 100.0f;
                reactor.FissionRate = power / reactor.MaxPowerOutput * 100.0f;
                reactor.AutoTemp = true;

#if SERVER
                if (GameMain.Server != null)
                {
                    reactorItem.CreateServerEvent(reactor);
                }
#endif
            }, null, true));

            commands.Add(new Command("oxygen|air", "oxygen/air: Replenishes the oxygen levels in every room to 100%.", (string[] args) =>
            {
                foreach (Hull hull in Hull.hullList)
                {
                    hull.OxygenPercentage = 100.0f;
                }
            }, null, true));

            commands.Add(new Command("kill", "kill [character]: Immediately kills the specified character.", (string[] args) =>
            {
                Character killedCharacter = (args.Length == 0) ? Character.Controlled : FindMatchingCharacter(args);
                killedCharacter.SetAllDamage(killedCharacter.MaxVitality * 2, 0.0f, 0.0f);
            },
            () =>
            {
                return new string[][]
                {
                    Character.CharacterList.Select(c => c.Name).Distinct().ToArray()
                };
            }));

            commands.Add(new Command("killmonsters", "killmonsters: Immediately kills all AI-controlled enemies in the level.", (string[] args) =>
            {
                foreach (Character c in Character.CharacterList)
                {
                    if (!(c.AIController is EnemyAIController)) continue;
                    c.SetAllDamage(c.MaxVitality, 0.0f, 0.0f);
                }
            }, null, true));

            commands.Add(new Command("netstats", "netstats: Toggles the visibility of the network statistics UI.", null));

            commands.Add(new Command("setclientcharacter", "setclientcharacter [client name] ; [character name]: Gives the client control of the specified character.", null,
            () =>
            {
                if (GameMain.NetworkMember == null) return null;

                return new string[][]
                {
        GameMain.NetworkMember.ConnectedClients.Select(c => c.Name).ToArray(),
        Character.CharacterList.Select(c => c.Name).Distinct().ToArray()
                };
            }));

            commands.Add(new Command("campaigninfo|campaignstatus", "campaigninfo: Display information about the state of the currently active campaign.", (string[] args) =>
            {
                var campaign = GameMain.GameSession?.GameMode as CampaignMode;
                if (campaign == null)
                {
                    ThrowError("No campaign active!");
                    return;
                }

                campaign.LogState();
            }));

            commands.Add(new Command("campaigndestination|setcampaigndestination", "campaigndestination [index]: Set the location to head towards in the currently active campaign.", (string[] args) =>
            {
                var campaign = GameMain.GameSession?.GameMode as CampaignMode;
                if (campaign == null)
                {
                    ThrowError("No campaign active!");
                    return;
                }

                if (args.Length == 0)
                {
                    int i = 0;
                    foreach (LocationConnection connection in campaign.Map.CurrentLocation.Connections)
                    {
                        NewMessage("     " + i + ". " + connection.OtherLocation(campaign.Map.CurrentLocation).Name, Color.White);
                        i++;
                    }
                    ShowQuestionPrompt("Select a destination (0 - " + (campaign.Map.CurrentLocation.Connections.Count - 1) + "):", (string selectedDestination) =>
                    {
                        int destinationIndex = -1;
                        if (!int.TryParse(selectedDestination, out destinationIndex)) return;
                        if (destinationIndex < 0 || destinationIndex >= campaign.Map.CurrentLocation.Connections.Count)
                        {
                            NewMessage("Index out of bounds!", Color.Red);
                            return;
                        }
                        Location location = campaign.Map.CurrentLocation.Connections[destinationIndex].OtherLocation(campaign.Map.CurrentLocation);
                        campaign.Map.SelectLocation(location);
                        NewMessage(location.Name + " selected.", Color.White);
                    });
                }
                else
                {
                    int destinationIndex = -1;
                    if (!int.TryParse(args[0], out destinationIndex)) return;
                    if (destinationIndex < 0 || destinationIndex >= campaign.Map.CurrentLocation.Connections.Count)
                    {
                        NewMessage("Index out of bounds!", Color.Red);
                        return;
                    }
                    Location location = campaign.Map.CurrentLocation.Connections[destinationIndex].OtherLocation(campaign.Map.CurrentLocation);
                    campaign.Map.SelectLocation(location);
                    NewMessage(location.Name + " selected.", Color.White);
                }
            }));

            commands.Add(new Command("difficulty|leveldifficulty", "difficulty [0-100]: Change the level difficulty setting in the server lobby.", null));

#if DEBUG
            commands.Add(new Command("savesubtoworkshop", "", (string[] args) =>
            {
                if (Submarine.MainSub == null) return;
                SteamManager.SaveToWorkshop(Submarine.MainSub);
            }));

            commands.Add(new Command("requestworkshopsubscriptions", "", (string[] args) =>
            {
                void itemsReceived(IList<Facepunch.Steamworks.Workshop.Item> items)
                {
                    foreach (var item in items)
                    {
                        Log("*********************************");
                        Log(item.Title);
                        Log(item.Description);
                        Log("Size: " + item.Size / 1024 +" kB");
                        Log("Directory: " + item.Directory);
                        Log("Installed: " + item.Installed);
                    }
                }

                SteamManager.GetWorkshopItems(itemsReceived);
            }));

            commands.Add(new Command("flipx", "flipx: mirror the main submarine horizontally", (string[] args) =>
            {
                Submarine.MainSub?.FlipX();
            }));
#endif

            InitProjectSpecific();

            commands.Sort((c1, c2) => c1.names[0].CompareTo(c2.names[0]));
        }

        private static string[] SplitCommand(string command)
        {
            command = command.Trim();

            List<string> commands = new List<string>();
            int escape = 0;
            bool inQuotes = false;
            string piece = "";
            
            for (int i = 0; i < command.Length; i++)
            {
                if (command[i] == '\\')
                {
                    if (escape == 0) escape = 2;
                    else piece += '\\';
                }
                else if (command[i] == '"')
                {
                    if (escape == 0) inQuotes = !inQuotes;
                    else piece += '"';
                }
                else if (command[i] == ' ' && !inQuotes)
                {
                    if (!string.IsNullOrWhiteSpace(piece)) commands.Add(piece);
                    piece = "";
                }
                else if (escape == 0) piece += command[i];

                if (escape > 0) escape--;
            }

            if (!string.IsNullOrWhiteSpace(piece)) commands.Add(piece); //add final piece

            return commands.ToArray();
        }

        public static string AutoComplete(string command)
        {
            string[] splitCommand = SplitCommand(command);
            string[] args = splitCommand.Skip(1).ToArray();

            //if an argument is given or the last character is a space, attempt to autocomplete the argument
            if (args.Length > 0 || (command.Length > 0 && command.Last() == ' '))
            {
                Command matchingCommand = commands.Find(c => c.names.Contains(splitCommand[0]));
                if (matchingCommand == null || matchingCommand.GetValidArgs == null) return command;

                int autoCompletedArgIndex = args.Length > 0 && command.Last() != ' ' ? args.Length - 1 : args.Length;

                //get all valid arguments for the given command
                string[][] allArgs = matchingCommand.GetValidArgs();
                if (allArgs == null || allArgs.GetLength(0) < autoCompletedArgIndex + 1) return command;

                if (string.IsNullOrEmpty(currentAutoCompletedCommand))
                {
                    currentAutoCompletedCommand = autoCompletedArgIndex > args.Length - 1 ? " " : args.Last();
                }

                //find all valid autocompletions for the given argument
                string[] validArgs = allArgs[autoCompletedArgIndex].Where(arg => 
                    currentAutoCompletedCommand.Trim().Length <= arg.Length && 
                    arg.Substring(0, currentAutoCompletedCommand.Trim().Length).ToLower() == currentAutoCompletedCommand.Trim().ToLower()).ToArray();

                if (validArgs.Length == 0) return command;

                currentAutoCompletedIndex = currentAutoCompletedIndex % validArgs.Length;
                string autoCompletedArg = validArgs[currentAutoCompletedIndex++];

                //add quotation marks to args that contain spaces
                if (autoCompletedArg.Contains(' ')) autoCompletedArg = '"' + autoCompletedArg + '"';
                for (int i = 0; i < splitCommand.Length; i++)
                {
                    if (splitCommand[i].Contains(' ')) splitCommand[i] = '"' + splitCommand[i] + '"';
                }

                return string.Join(" ", autoCompletedArgIndex >= args.Length ? splitCommand : splitCommand.Take(splitCommand.Length - 1)) + " " + autoCompletedArg;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(currentAutoCompletedCommand))
                {
                    currentAutoCompletedCommand = command;
                }

                List<string> matchingCommands = new List<string>();
                foreach (Command c in commands)
                {
                    foreach (string name in c.names)
                    {
                        if (currentAutoCompletedCommand.Length > name.Length) continue;
                        if (currentAutoCompletedCommand == name.Substring(0, currentAutoCompletedCommand.Length))
                        {
                            matchingCommands.Add(name);
                        }
                    }
                }

                if (matchingCommands.Count == 0) return command;

                currentAutoCompletedIndex = currentAutoCompletedIndex % matchingCommands.Count;
                return matchingCommands[currentAutoCompletedIndex++];
            }
        }

        private static string AutoCompleteStr(string str, IEnumerable<string> validStrings)
        {
            if (string.IsNullOrEmpty(str)) return str;
            foreach (string validStr in validStrings)
            {
                if (validStr.Length > str.Length && validStr.Substring(0, str.Length) == str) return validStr;
            }
            return str;
        }

        public static void ResetAutoComplete()
        {
            currentAutoCompletedCommand = "";
            currentAutoCompletedIndex = 0;
        }

        public static string SelectMessage(int direction, string currentText = null)
        {
            if (Messages.Count == 0) return "";

            direction = MathHelper.Clamp(direction, -1, 1);

			int i = 0;
			do
			{
				selectedIndex += direction;
				if (selectedIndex < 0) selectedIndex = Messages.Count - 1;
				selectedIndex = selectedIndex % Messages.Count;
				if (++i >= Messages.Count) break;
			} while (!Messages[selectedIndex].IsCommand || Messages[selectedIndex].Text == currentText);

            return Messages[selectedIndex].Text;            
        }

        public static void ExecuteCommand(string command)
        {
            if (activeQuestionCallback != null)
            {
#if CLIENT
                activeQuestionText = null;
#endif
                NewMessage(command, Color.White, true);
                //reset the variable before invoking the delegate because the method may need to activate another question
                var temp = activeQuestionCallback;
                activeQuestionCallback = null;
                temp(command);
                return;
            }

            if (string.IsNullOrWhiteSpace(command) || command == "\\" || command == "\n") return;

            string[] splitCommand = SplitCommand(command);
            if (splitCommand.Length == 0)
            {
                ThrowError("Failed to execute command \"" + command + "\"!");
                GameAnalyticsManager.AddErrorEventOnce(
                    "DebugConsole.ExecuteCommand:LengthZero",
                    GameAnalyticsSDK.Net.EGAErrorSeverity.Error,
                    "Failed to execute command \"" + command + "\"!");
                return;
            }

            if (!splitCommand[0].ToLowerInvariant().Equals("admin"))
            {
                NewMessage(command, Color.White, true);
            }
            
#if CLIENT
            if (GameMain.Client != null)
            {
                if (GameMain.Client.HasConsoleCommandPermission(splitCommand[0].ToLowerInvariant()))
                {
                    Command matchingCommand = commands.Find(c => c.names.Contains(splitCommand[0].ToLowerInvariant()));

                    //if the command is not defined client-side, we'll relay it anyway because it may be a custom command at the server's side
                    if (matchingCommand == null || matchingCommand.RelayToServer)
                    {
                        GameMain.Client.SendConsoleCommand(command);
                    }
                    else
                    {
                        matchingCommand.ClientExecute(splitCommand.Skip(1).ToArray());
                    }

                    NewMessage("Server command: " + command, Color.White);
                    return;
                }
#if !DEBUG
                if (!IsCommandPermitted(splitCommand[0].ToLowerInvariant(), GameMain.Client))
                {
                    ThrowError("You're not permitted to use the command \"" + splitCommand[0].ToLowerInvariant() + "\"!");
                    return;
                }
#endif
            }
#endif

            bool commandFound = false;
            foreach (Command c in commands)
            {
                if (!c.names.Contains(splitCommand[0].ToLowerInvariant())) continue;                
                c.Execute(splitCommand.Skip(1).ToArray());
                commandFound = true;
                break;                
            }

            if (!commandFound)
            {
                ThrowError("Command \"" + splitCommand[0] + "\" not found.");
            }
        }
        
        private static Character FindMatchingCharacter(string[] args, bool ignoreRemotePlayers = false)
        {
            if (args.Length == 0) return null;

            int characterIndex;
            string characterName;
            if (int.TryParse(args.Last(), out characterIndex) && args.Length > 1)
            {
                characterName = string.Join(" ", args.Take(args.Length - 1)).ToLowerInvariant();
            }
            else
            {
                characterName = string.Join(" ", args).ToLowerInvariant();
                characterIndex = -1;
            }

            var matchingCharacters = Character.CharacterList.FindAll(c => (!ignoreRemotePlayers || !c.IsRemotePlayer) && c.Name.ToLowerInvariant() == characterName);

            if (!matchingCharacters.Any())
            {
                NewMessage("Character \""+ characterName + "\" not found", Color.Red);
                return null;
            }

            if (characterIndex == -1)
            {
                if (matchingCharacters.Count > 1)
                {
                    NewMessage(
                        "Found multiple matching characters. " +
                        "Use \"[charactername] [0-" + (matchingCharacters.Count - 1) + "]\" to choose a specific character.",
                        Color.LightGray);
                }
                return matchingCharacters[0];
            }
            else if (characterIndex < 0 || characterIndex >= matchingCharacters.Count)
            {
                ThrowError("Character index out of range. Select an index between 0 and " + (matchingCharacters.Count - 1));
            }
            else
            {
                return matchingCharacters[characterIndex];
            }

            return null;
        }

        private static void SpawnCharacter(string[] args, Vector2 cursorWorldPos, out string errorMsg)
        {
            errorMsg = "";
            if (args.Length == 0) return;

            Character spawnedCharacter = null;

            Vector2 spawnPosition = Vector2.Zero;
            WayPoint spawnPoint = null;

            if (args.Length > 1)
            {
                switch (args[1].ToLowerInvariant())
                {
                    case "inside":
                        spawnPoint = WayPoint.GetRandom(SpawnType.Human, null, Submarine.MainSub);
                        break;
                    case "outside":
                        spawnPoint = WayPoint.GetRandom(SpawnType.Enemy);
                        break;
                    case "near":
                    case "close":
                        float closestDist = -1.0f;
                        foreach (WayPoint wp in WayPoint.WayPointList)
                        {
                            if (wp.Submarine != null) continue;

                            //don't spawn inside hulls
                            if (Hull.FindHull(wp.WorldPosition, null) != null) continue;

                            float dist = Vector2.Distance(wp.WorldPosition, GameMain.GameScreen.Cam.WorldViewCenter);

                            if (closestDist < 0.0f || dist < closestDist)
                            {
                                spawnPoint = wp;
                                closestDist = dist;
                            }
                        }
                        break;
                    case "cursor":
                        spawnPosition = cursorWorldPos;
                        break;
                    default:
                        spawnPoint = WayPoint.GetRandom(args[0].ToLowerInvariant() == "human" ? SpawnType.Human : SpawnType.Enemy);
                        break;
                }
            }
            else
            {
                spawnPoint = WayPoint.GetRandom(args[0].ToLowerInvariant() == "human" ? SpawnType.Human : SpawnType.Enemy);
            }

            if (string.IsNullOrWhiteSpace(args[0])) return;

            if (spawnPoint != null) spawnPosition = spawnPoint.WorldPosition;

            if (args[0].ToLowerInvariant() == "human")
            {
                spawnedCharacter = Character.Create(Character.HumanConfigFile, spawnPosition, ToolBox.RandomSeed(8));
                if (GameMain.GameSession != null)
                {
                    if (GameMain.GameSession.GameMode != null && !GameMain.GameSession.GameMode.IsSinglePlayer)
                    {
                        //TODO: a way to select which team to spawn to?
                        spawnedCharacter.TeamID = Character.Controlled != null ? Character.Controlled.TeamID : (byte)1;
                    }
#if CLIENT
                    GameMain.GameSession.CrewManager.AddCharacter(spawnedCharacter);          
#endif
                }
            }
            else
            {
                IEnumerable<string> characterFiles = GameMain.Instance.GetFilesOfType(ContentType.Character);
                foreach (string characterFile in characterFiles)
                {
                    if (Path.GetFileNameWithoutExtension(characterFile).ToLowerInvariant() == args[0].ToLowerInvariant())
                    {
                        Character.Create(characterFile, spawnPosition, ToolBox.RandomSeed(8));
                        return;
                    }
                }

                errorMsg = "No character matching the name \"" + args[0] + "\" found in the selected content package.";

                //attempt to open the config from the default path (the file may still be present even if it isn't included in the content package)
                string configPath = "Content/Characters/"
                    + args[0].First().ToString().ToUpper() + args[0].Substring(1)
                    + "/" + args[0].ToLower() + ".xml";
                Character.Create(configPath, spawnPosition, ToolBox.RandomSeed(8));
            }
        }

        private static void SpawnItem(string[] args, Vector2 cursorPos, Character controlledCharacter, out string errorMsg)
        {
            errorMsg = "";
            if (args.Length < 1) return;

            Vector2? spawnPos = null;
            Inventory spawnInventory = null;
            
            int extraParams = 0;
            switch (args.Last().ToLowerInvariant())
            {
                case "cursor":
                    extraParams = 1;
                    spawnPos = cursorPos;
                    break;
                case "inventory":
                    extraParams = 1;
                    spawnInventory = controlledCharacter?.Inventory;
                    break;
                case "cargo":
                    var wp = WayPoint.GetRandom(SpawnType.Cargo, null, Submarine.MainSub);
                    spawnPos = wp == null ? Vector2.Zero : wp.WorldPosition;
                    break;
                //Dont do a thing, random is basically Human points anyways - its in the help description.
                case "random":
                    extraParams = 1;
                    return;
                default:
                    extraParams = 0;
                    break;
            }

            string itemName = string.Join(" ", args.Take(args.Length - extraParams)).ToLowerInvariant();

            ItemPrefab itemPrefab = MapEntityPrefab.Find(itemName) as ItemPrefab;
            if (itemPrefab == null && extraParams == 0)
            {
#if SERVER
                if (GameMain.Server != null)
                {
                    var client = GameMain.Server.ConnectedClients.Find(c => c.Name.ToLower() == args.Last().ToLower());
                    if (client != null)
                    {
                        extraParams += 1;
                        itemName = string.Join(" ", args.Take(args.Length - extraParams)).ToLowerInvariant();
                        if (client.Character != null && client.Character.Name == args.Last().ToLower()) spawnInventory = client.Character.Inventory;
                        itemPrefab = MapEntityPrefab.Find(itemName) as ItemPrefab;
                    }
                }
#endif
            }
            //Check again if the item can be found again after having checked for a character
            if (itemPrefab == null)
            {
                errorMsg = "Item \"" + itemName + "\" not found!";
                return;
            }

            if ((spawnPos == null || spawnPos == Vector2.Zero) && spawnInventory == null)
            {
                var wp = WayPoint.GetRandom(SpawnType.Human, null, Submarine.MainSub);
                spawnPos = wp == null ? Vector2.Zero : wp.WorldPosition;
            }

            if (spawnPos != null)
            {
                Entity.Spawner.AddToSpawnQueue(itemPrefab, (Vector2)spawnPos);

            }
            else if (spawnInventory != null)
            {
                Entity.Spawner.AddToSpawnQueue(itemPrefab, spawnInventory);
            }
        }

        public static void NewMessage(string msg, Color color, bool isCommand = false)
        {
            if (string.IsNullOrEmpty((msg))) return;

#if SERVER
            var newMsg = new ColoredText(msg, color, isCommand);
            Messages.Add(newMsg);

            //TODO: REMOVE
            Console.ForegroundColor = XnaToConsoleColor.Convert(color);
            Console.WriteLine(msg);
            Console.ForegroundColor = ConsoleColor.White;

            if (GameSettings.SaveDebugConsoleLogs)
            {
                unsavedMessages.Add(newMsg);
                if (unsavedMessages.Count >= messagesPerFile)
                {
                    SaveLogs();
                    unsavedMessages.Clear();
                }
            }

            if (Messages.Count > MaxMessages)
            {
                Messages.RemoveRange(0, Messages.Count - MaxMessages);
            }
#elif CLIENT
            lock (queuedMessages)
            {
                queuedMessages.Enqueue(new ColoredText(msg, color, isCommand));
            }
#endif
        }

        public static void ShowQuestionPrompt(string question, QuestionCallback onAnswered)
        {
#if CLIENT
            activeQuestionText = new GUITextBlock(new RectTransform(new Point(listBox.Content.Rect.Width, 0), listBox.Content.RectTransform),
                "   >>" + question, font: GUI.SmallFont, wrap: true)
            {
                CanBeFocused = false,
                TextColor = Color.Cyan
            };
#else
            NewMessage("   >>" + question, Color.Cyan);
#endif
            activeQuestionCallback += onAnswered;
        }

        private static bool TryParseTimeSpan(string s, out TimeSpan timeSpan)
        {
            timeSpan = new TimeSpan();
            if (string.IsNullOrWhiteSpace(s)) return false;

            string currNum = "";
            foreach (char c in s)
            {
                if (char.IsDigit(c))
                {
                    currNum += c;
                }
                else if (char.IsWhiteSpace(c))
                {
                    continue;
                }
                else
                {
                    int parsedNum = 0;
                    if (!int.TryParse(currNum, out parsedNum))
                    {
                        return false;
                    }

                    switch (c)
                    {
                        case 'd':
                            timeSpan += new TimeSpan(parsedNum, 0, 0, 0, 0);
                            break;
                        case 'h':
                            timeSpan += new TimeSpan(0, parsedNum, 0, 0, 0);
                            break;
                        case 'm':
                            timeSpan += new TimeSpan(0, 0, parsedNum, 0, 0);
                            break;
                        case 's':
                            timeSpan += new TimeSpan(0, 0, 0, parsedNum, 0);
                            break;
                        default:
                            return false;
                    }

                    currNum = "";
                }
            }

            return true;
        }

        public static Command FindCommand(string commandName)
        {
            commandName = commandName.ToLowerInvariant();
            return commands.Find(c => c.names.Any(n => n.ToLowerInvariant() == commandName));
        }


        public static void Log(string message)
        {
            if (GameSettings.VerboseLogging) NewMessage(message, Color.Gray);
        }

        public static void ThrowError(string error, Exception e = null)
        {
            if (e != null)
            {
                error += " {" + e.Message + "}\n" + e.StackTrace;
            }
            System.Diagnostics.Debug.WriteLine(error);
            NewMessage(error, Color.Red);
#if CLIENT
            isOpen = true;
#endif
        }


        public static void SaveLogs()
        {
            if (unsavedMessages.Count == 0) return;
            if (!Directory.Exists(SavePath))
            {
                try
                {
                    Directory.CreateDirectory(SavePath);
                }
                catch (Exception e)
                {
                    ThrowError("Failed to create a folder for debug console logs", e);
                    return;
                }
            }

            string fileName = "DebugConsoleLog_" + DateTime.Now.ToShortDateString() + "_" + DateTime.Now.ToShortTimeString() + ".txt";
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (char invalidChar in invalidChars)
            {
                fileName = fileName.Replace(invalidChar.ToString(), "");
            }

            string filePath = Path.Combine(SavePath, fileName);
            if (File.Exists(filePath))
            {
                int fileNum = 2;
                while (File.Exists(filePath + " (" + fileNum + ")"))
                {
                    fileNum++;
                }
                filePath = filePath + " (" + fileNum + ")";
            }

            try
            {
                File.WriteAllLines(filePath, unsavedMessages.Select(l => "[" + l.Time + "] " + l.Text));
            }
            catch (Exception e)
            {
                unsavedMessages.Clear();
                ThrowError("Saving debug console log to " + filePath + " failed", e);
            }
        }
    }
}
