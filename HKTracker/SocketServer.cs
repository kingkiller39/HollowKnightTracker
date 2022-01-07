using System.Collections.Generic;
using System.Linq;
using System.IO;
using Modding;
using WebSocketSharp;
using WebSocketSharp.Server;
using UnityEngine;
namespace HKTracker
{
    internal class SocketServer : WebSocketBehavior
    {
        public SocketServer()
        {
            IgnoreExtensions = true;
            randoAtBench = false;
        }

        private static readonly HashSet<string> IntKeysToSend = new HashSet<string> { "simpleKeys", "nailDamage", "maxHealth", "MPReserveMax", "ore", "rancidEggs", "grubsCollected", "charmSlotsFilled", "charmSlots", "flamesCollected", "guardiansDefeated" };
        private bool randoAtBench { get; set; }
        public static readonly string RandoTrackLogFile = Path.Combine(Application.persistentDataPath, "Randomizer 4", "Recent", "TrackerLog.txt");
        public void Broadcast(string s)
        {
            Sessions.Broadcast(s);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            if (State != WebSocketState.Open) return;

            switch (e.Data)
            {
                case "mods":
                    Send(JsonUtility.ToJson(ModHooks.LoadedModsWithVersions));
                    break;
                case "version":
                    Send($"{{ \"version\":\"{HKTracker.Instance.GetVersion()}\" }}");
                    break;
                case "json":
                    Send(GetJson());
                    GetRandom();
                    break;
                default:
                    if (e.Data.Contains('|'))
                    {
                        switch (e.Data.Split('|')[0])
                        {
                            case "bool":
                                string b = PlayerData.instance.GetBool(e.Data.Split('|')[1]).ToString();
                                SendMessage(e.Data.Split('|')[1], b);
                                break;
                            case "int":
                                string i = PlayerData.instance.GetInt(e.Data.Split('|')[1]).ToString();
                                SendMessage(e.Data.Split('|')[1], i);
                                break;
                        }
                    }
                    else
                    {
                        Send("mods,version,json,bool|{var},int|{var}");
                    }
                    break;
            }
        }

        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            HKTracker.Instance.LogError(e.Message);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);

            ModHooks.NewGameHook -= NewGame;
            ModHooks.AfterSavegameLoadHook -= LoadSave;
            ModHooks.SetPlayerBoolHook -= EchoBool;
            ModHooks.SetPlayerIntHook -= EchoInt;
            On.GameMap.Start -= gameMapStart;
            ModHooks.ApplicationQuitHook -= OnQuit;
            HKTracker.Instance.Log("CLOSE: Code:" + e.Code + ", Reason:" + e.Reason);
        }



        protected override void OnOpen()
        {
            HKTracker.Instance.Log("OPEN");
        }

        public void SendMessage(string var, string value)
        {
            if (State != WebSocketState.Open) return;
            Send(new Row(var, value).ToJsonElementPair);
        }

        public void LoadSave(SaveGameData data)
        {
            if (State != WebSocketState.Open) return;
            HKTracker.Instance.LogDebug("Loaded Save");
            //GetRandom();
            SendMessage("SaveLoaded", "true");
        }
        public void LoadSave()
        {
            if (State != WebSocketState.Open) return;
            //GetRandom();
            SendMessage("SaveLoaded", "true");
        }

        public bool EchoBool(string var, bool value)
        {
            
            HKTracker.Instance.LogDebug($"EchoBool: {var} = {value}");
            if (var == "atBench" && value && !randoAtBench)
            {

                LoadSave();
                randoAtBench = true;
            }
            else if (var == "atBench" && !value && randoAtBench)
            {
                randoAtBench = false;
            }
            else if (var.StartsWith("gotCharm_") || var.StartsWith("brokenCharm_") || var.StartsWith("equippedCharm_") || var.StartsWith("can") ||var.StartsWith("has") || var.StartsWith("maskBroken") || var == "overcharmed" || var.StartsWith("used") || var.StartsWith("opened") || var.StartsWith("gave") || var == "unlockedCompletionRate")
            {
                SendMessage(var, value.ToString());
            }
            return value;
        }

        public int EchoInt(string var, int value)
        {
            HKTracker.Instance.LogDebug($"EchoInt: {var} = {value}");
            if (var == "royalCharmState" && (value == 1 || value == 2 || value == 3 || value == 4))
            {
                EchoBool("gotCharm_36", true);
            }
            if (IntKeysToSend.Contains(var) || var.EndsWith("Level") || var.StartsWith("trinket") || var == "nailSmithUpgrades" || var == "rancidEggs" || var == "royalCharmState" || var == "dreamOrbs")
            {
                SendMessage(var, value.ToString());
            }
            return value;
        }

        public string GetJson()
        {
            PlayerData playerData = PlayerData.instance;
            string json = JsonUtility.ToJson(playerData);
            return json;
        }
        public void GetRandom()
        {
            if (Modding.ModHooks.GetMod("RandomizerMod", false, false) is not Mod)
            {
                SendMessage("canFocus", "true");
                SendMessage("swim", "true");
                SendMessage("elevatorPass", "true");
                SendMessage("FullNail", "true");
                return;
            }
            string seed = "";
            bool RandomizeSwim = false;
            bool RandomizeElevatorPass = false;
            bool RandomizeFocus = false;
            bool RandomizeNail = false;

            if (File.Exists(RandoTrackLogFile))
            {
                using (var reader = new StreamReader(RandoTrackLogFile))
                {
                    bool foundSeed = false;
                    bool foundRandomizeSwim = false;
                    bool foundRandomizeElevatorPass = false;
                    bool foundRandomizeNail = false;
                    bool foundRandomizeFocus = false;
                    string temp;
                    while(!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        if (line.Trim().StartsWith("\"Seed\""))
                        {
                            seed = line.Split(':')[1].Trim();
                            if (seed.Contains(','))
                            {
                                seed = seed.Remove(seed.Length - 1, 1);
                            }
                            foundSeed = true;
                        }
                        else if (line.Trim().StartsWith("\"RandomizeSwim\""))
                        {
                            temp = line.Split(':')[1].Trim();
                            if (temp.Contains(','))
                            {
                                temp = temp.Remove(temp.Length - 1, 1);
                            }
                            RandomizeSwim = System.Convert.ToBoolean(temp);
                            foundRandomizeSwim = true;
                        }
                        else if (line.Trim().StartsWith("\"RandomizeElevatorPass\""))
                        {
                            temp = line.Split(':')[1].Trim();
                            if (temp.Contains(','))
                            {
                                temp = temp.Remove(temp.Length - 1, 1);
                            }
                            RandomizeElevatorPass = System.Convert.ToBoolean(temp);
                            foundRandomizeElevatorPass = true;
                        }
                        else if (line.Trim().StartsWith("\"RandomizeNail\""))
                        {
                            temp = line.Split(':')[1].Trim();
                            if (temp.Contains(','))
                            {
                                temp = temp.Remove(temp.Length - 1, 1);
                            }
                            RandomizeNail = System.Convert.ToBoolean(temp);
                            foundRandomizeNail = true;
                        }
                        else if (line.Trim().StartsWith("\"RandomizeFocus\""))
                        {
                            temp = line.Split(':')[1].Trim();
                            if (temp.Contains(','))
                            {
                                temp = temp.Remove(temp.Length - 1, 1);
                            }
                            RandomizeFocus = System.Convert.ToBoolean(temp);
                            foundRandomizeFocus = true;
                        }
                        if (foundSeed && foundRandomizeSwim && foundRandomizeElevatorPass && foundRandomizeNail && foundRandomizeFocus)
                        {
                            break;
                        }
                    }
                }
            }
            SendMessage("seed", seed);
            if (!RandomizeSwim) { SendMessage("swim", "true"); }
            if (!RandomizeElevatorPass) { SendMessage("elevatorPass", "true"); }
            if (!RandomizeFocus) { SendMessage("canFocus", "true"); }
            if (!RandomizeNail)
            {
                SendMessage("FullNail", "true");
            }
            else
            {
                SendMessage("canDownslash", "true");
            }
        }


        public void NewGame()
        {
            if (State != WebSocketState.Open) return;
            HKTracker.Instance.LogDebug("Loaded New Save");
            //GetRandom();
            SendMessage("NewSave", "true");
        }

        public void gameMapStart(On.GameMap.orig_Start orig, GameMap self)
        {
            orig(self);
            if (State != WebSocketState.Open) return;
            //GetRandom();
            SendMessage("NewSave", "true");
        }

        public void OnQuit()
        {
            if (State != WebSocketState.Open) return;
            SendMessage("GameExiting", "true");
        }

        public struct Row
        {
            // ReSharper disable once InconsistentNaming
            public string var { get; set; }
            // ReSharper disable once InconsistentNaming
            public object value { get; set; }

            public Row(string var, object value)
            {
                this.var = var;
                this.value = value;
            }

            public string ToJsonElementPair => " { \"var\" : \"" + var + "\",  \"value\" :  \"" + value + "\" }";
            public string ToJsonElement => $"\"{var}\" : \"{value}\"";
        }

    }
}
