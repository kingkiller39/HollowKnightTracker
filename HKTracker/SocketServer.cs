﻿using System.Collections.Generic;
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
        public static string seed = "";
        public static bool RandomizeSwim = false;
        public static bool RandomizeElevatorPass = false;
        public static bool RandomizeFocus = false;
        public static bool RandomizeNail = false;
        public static bool RandomizeSplitDash = false;
        public static bool RandomizeSplitClaw = false;
        public static bool RandomizeCDash = false;
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
                    getSwim();
                    getEPass();
                    getFocus();
                    GetNail();
                    GetDash();
                    GetClaw();
                    getCDash();
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
            GetRandoSettings();
            SendMessage("SaveLoaded", "true");
        }
        public void LoadSave()
        {
            if (State != WebSocketState.Open) return;
            //GetRandom();
            GetRandoSettings();
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

        public void CheckPD()
        {
            PlayerData PD = PlayerData.instance;
            HKTracker hK = HKTracker.Instance;
            hK.Log("swim: " + (PD.GetBool("canSwim") || PD.GetBool("Swim")));
            hK.Log("epass: " + (PD.GetBool("hasElevatorPass") || PD.GetBool("Elevator_Pass")));
            hK.Log("focus: " + (PD.GetBool("canFocus") || PD.GetBool("Focus")));
            hK.Log("LNail: " + (PD.GetBool("Leftslash") || PD.GetBool("canSideslashLeft")));
            hK.Log("RNail: " + (PD.GetBool("Rightslash") || PD.GetBool("canSideslashRight")));
            hK.Log("UNail: " + (PD.GetBool("canUpslash") || PD.GetBool("Upslash")));
            hK.Log("DNail: " + (PD.GetBool("canDownslash") || PD.GetBool("Downslash")));
            hK.Log("LDash: " + (PD.GetBool("canDashLeft") || PD.GetBool("Left_Mothwing_Cloak")));
            hK.Log("RDash: " + (PD.GetBool("canDashRight") || PD.GetBool("Right_Mothwing_Cloak")));
            hK.Log("LClaw: " + (PD.GetBool("hasWalljumpLeft") || PD.GetBool("Left_Mantis_Claw")));
            hK.Log("RClaw: " + (PD.GetBool("hasWalljumpRight") || PD.GetBool("Right_Mantis_Claw")));
            hK.Log("LCDash: " + (PD.GetBool("hasSuperdashLeft") || PD.GetBool("Left_Crystal_Heart")));
            hK.Log("RCDash: " + (PD.GetBool("hasSuperdashRight") || PD.GetBool("Right_Crystal_Heart")));
        }
        public void getSwim()
        {
            if (!RandomizeSwim) { SendMessage("swim", "true"); }
            else { SendMessage("swim", (PlayerData.instance.GetBool("canSwim") || PlayerData.instance.GetBool("Swim")).ToString()); }
        }

        public void getEPass()
        {
            if (!RandomizeElevatorPass) { SendMessage("elevatorPass", "true"); }
            else { SendMessage("elevatorPass", (PlayerData.instance.GetBool("hasElevatorPass") || PlayerData.instance.GetBool("Elevator_Pass")).ToString()); }
        }

        public void getFocus()
        {
            
            if (!RandomizeFocus) { SendMessage("canFocus", "true"); }
            else { SendMessage("canFocus", (PlayerData.instance.GetBool("canFocus") || PlayerData.instance.GetBool("Focus")).ToString()); }
        }

        public void GetNail()
        {
            if (!RandomizeNail) { SendMessage("FullNail", "true"); }
            else
            {
                SendMessage("canSideslashRight", (PlayerData.instance.GetBool("Rightslash") || PlayerData.instance.GetBool("canSideslashRight")).ToString());
                SendMessage("canUpslash", (PlayerData.instance.GetBool("canUpslash") || PlayerData.instance.GetBool("Upslash")).ToString());
                SendMessage("canSideslashLeft", (PlayerData.instance.GetBool("Leftslash") || PlayerData.instance.GetBool("canSideslashLeft")).ToString());
            }
        }

        public void GetDash()
        {
            if (!RandomizeSplitDash || PlayerData.instance.GetBool("hasDash")) { return; }
            else
            {
                SendMessage("canDashLeft", (PlayerData.instance.GetBool("canDashLeft") || PlayerData.instance.GetBool("Left_Mothwing_Cloak")).ToString());
                SendMessage("canDashRight", (PlayerData.instance.GetBool("canDashRight") || PlayerData.instance.GetBool("Right_Mothwing_Cloak")).ToString());
            }
        }

        public void GetClaw()
        {
            if (!RandomizeSplitClaw || PlayerData.instance.GetBool("hasWalljump")) { return; }
            else
            {
                SendMessage("hasWalljumpLeft", (PlayerData.instance.GetBool("hasWalljumpLeft") || PlayerData.instance.GetBool("Left_Mantis_Claw")).ToString());
                SendMessage("hasWalljumpRight", (PlayerData.instance.GetBool("hasWalljumpRight") || PlayerData.instance.GetBool("Right_Mantis_Claw")).ToString());
            }
        }

        public void getCDash()
        {
            if (!RandomizeCDash || PlayerData.instance.GetBool("hasSuperDash")) { return; }
            else
            {
                SendMessage("hasSuperdashLeft", (PlayerData.instance.GetBool("hasSuperdashLeft") || PlayerData.instance.GetBool("Left_Crystal_Heart")).ToString());
                SendMessage("hasSuperdashRight", (PlayerData.instance.GetBool("hasSuperdashRight") || PlayerData.instance.GetBool("Right_Crystal_Heart")).ToString());
            }
        }

        public bool CheckForItem(string name)
        {
            if (File.Exists(RandoTrackLogFile))
            {
                using (FileStream fs = new FileStream(RandoTrackLogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = new StreamReader(fs))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            if (line.Contains(name)) { return true; }
                        }
                    }
                }
            }
            return false;
        }

        public string[] CheckForItem(string name1, string name2)
        {
            bool found1 = false;
            bool found2 = false;
            string[] FoundString = new string[] { "false", "false" };
            if (File.Exists(RandoTrackLogFile))
            {
                using (FileStream fs = new FileStream(RandoTrackLogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = new StreamReader(fs))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            if (line.Contains(name1))
                            {
                                found1 = true;
                                FoundString[0] = "true";
                            }
                            else if (line.Contains(name2))
                            {
                                found2 = true;
                                FoundString[1] = "true";
                            }
                            if (found1 && found2) { return FoundString; }
                        }
                    }
                }
            }
            return FoundString;
        }

        public string[] CheckForItem(string name1, string name2, string name3)
        {
            bool found1 = false;
            bool found2 = false;
            bool found3 = false;
            string[] FoundString = new string[] { "false", "false", "false" };
            if (File.Exists(RandoTrackLogFile))
            {
                using (FileStream fs = new FileStream(RandoTrackLogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = new StreamReader(fs))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            if (line.Contains(name1))
                            {
                                found1 = true;
                                FoundString[0] = "true";
                            }
                            else if (line.Contains(name2))
                            {
                                found2 = true;
                                FoundString[1] = "true";
                            }
                            else if (line.Contains(name3))
                            {
                                found3 = true;
                                FoundString[2] = "true";
                            }
                            if (found1 && found2 && found3) { return FoundString; }
                        }
                    }
                }
            }
            return FoundString;
        }

        public static void GetRandoSettings()
        {
            seed = "";
            RandomizeSwim = false;
            RandomizeElevatorPass = false;
            RandomizeFocus = false;
            RandomizeNail = false;
            RandomizeSplitDash = false;
            RandomizeSplitClaw = false;
            RandomizeCDash = false;

            if (File.Exists(RandoTrackLogFile))
            {
                using (FileStream fs = new FileStream(RandoTrackLogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = new StreamReader(fs))
                    {
                        bool foundSeed = false;
                        bool foundRandomizeSwim = false;
                        bool foundRandomizeElevatorPass = false;
                        bool foundRandomizeNail = false;
                        bool foundRandomizeFocus = false;
                        bool foundRandomizeSplitDash = false;
                        bool foundRandomizeSplitClaw = false;
                        bool foundRandomizeCDash = false;
                        string temp;
                        while (!reader.EndOfStream)
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
                            else if (line.Trim().StartsWith("\"SplitCloak\""))
                            {
                                temp = line.Split(':')[1].Trim();
                                if (temp.Contains(','))
                                {
                                    temp = temp.Remove(temp.Length - 1, 1);
                                }
                                RandomizeSplitDash = System.Convert.ToBoolean(temp);
                                foundRandomizeSplitDash = true;
                            }
                            else if (line.Trim().StartsWith("\"SplitClaw\""))
                            {
                                temp = line.Split(':')[1].Trim();
                                if (temp.Contains(','))
                                {
                                    temp = temp.Remove(temp.Length - 1, 1);
                                }
                                RandomizeSplitClaw = System.Convert.ToBoolean(temp);
                                foundRandomizeSplitClaw = true;
                            }
                            else if (line.Trim().StartsWith("\"SplitSuperdash\""))
                            {
                                temp = line.Split(':')[1].Trim();
                                if (temp.Contains(','))
                                {
                                    temp = temp.Remove(temp.Length - 1, 1);
                                }
                                RandomizeCDash = System.Convert.ToBoolean(temp);
                                foundRandomizeCDash = true;
                            }
                            if (foundSeed && foundRandomizeSwim && foundRandomizeElevatorPass && foundRandomizeNail && foundRandomizeFocus && foundRandomizeSplitDash && foundRandomizeSplitClaw && foundRandomizeCDash)
                            {
                                break;
                            }
                        }
                    }
                }
            }
        }
        public void GetRandom()
        {
            if (Modding.ModHooks.GetMod("Randomizer 4", false, false) is not Mod)
            {
                seed = "";
                RandomizeSwim = false;
                RandomizeElevatorPass = false;
                RandomizeFocus = false;
                RandomizeNail = false;
                SendMessage("canFocus", "true");
                SendMessage("swim", "true");
                SendMessage("elevatorPass", "true");
                SendMessage("FullNail", "true");
                return;
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
            GetRandoSettings();
            SendMessage("NewSave", "true");
        }

        public void gameMapStart(On.GameMap.orig_Start orig, GameMap self)
        {
            orig(self);
            if (State != WebSocketState.Open) return;
            //GetRandom();
            GetRandoSettings();
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
