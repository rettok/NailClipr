﻿using EliteMMO.API;
using NailClipr.Classes;
using System;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;

namespace NailClipr
{
    class Functions
    {
        

        public static void AddZonePoint(Structs.WarpPoint wp)
        {
            NailClipr.GUI_WARP.Items.Add(wp.title);
            Structs.zonePoints.Add(wp);
        }
        public static void ClearZonePoints()
        {
            NailClipr.GUI_WARP.Text = "";
            NailClipr.GUI_WARP.Items.Clear();
            Structs.zonePoints.Clear();
        }
        public static void LoadZonePoints(EliteAPI api)
        {
            Structs.warpPoints.ForEach(wp =>
            {
                if (wp.zone == api.Player.ZoneId)
                {
                    Structs.zonePoints.Add(wp);
                    NailClipr.GUI_WARP.Items.Add(wp.title);
                }
            });
        }
        public static void PlayersRendered(EliteAPI api)
        {
            bool findPlayer = Structs.settings.playerDetection;
            int count = 0;

            const Int32 PC = 0x0001;
            const Int32 NPC = 0x0002;
            const Int32 Mob = 0x0010;
            const Int32 Self = 0x000D;

            for (var x = 0; x < 4096; x++)
            {
                var entity = api.Entity.GetEntity(x);
                bool invalid = entity.WarpPointer == 0,
                    dead = entity.HealthPercent <= 0,
                    outsideRange = entity.Distance > 50.0f || float.IsNaN(entity.Distance) || entity.Distance <= 0,
                    isRendered = (entity.Render0000 & 0x200) == 0x200,
                    isSelf = (entity.SpawnFlags & Self) == Self || entity.Name == api.Player.Name,
                    isMob = (entity.SpawnFlags & Mob) == Mob,
                    isNPC = (entity.SpawnFlags & NPC) == NPC,
                    isPC = (entity.SpawnFlags & PC) == PC,
                    invalidPlayerName = isPC && (entity.Name.Length < Structs.FFXI.Name.MINLENGTH || entity.Name.Length > Structs.FFXI.Name.MAXLENGTH || !Regex.IsMatch(entity.Name, @"^[a-zA-Z]+$")),
                    inWhitelist = isPC && Structs.Speed.whitelist.IndexOf(entity.Name) != -1;

                if (invalid || dead || outsideRange || !isRendered || isSelf || inWhitelist || invalidPlayerName)
                    continue;

                if (isPC && findPlayer)
                {
                    count++;
                    Player.isAlone = false;

                    bool closerPC = Updates.nearestPC.distance == 0 || entity.Distance < Updates.nearestPC.distance || entity.Name == Updates.nearestPC.name;
                    if (closerPC)
                    {
                        Updates.nearestPC.name = entity.Name;
                        Updates.nearestPC.distance = entity.Distance;
                    }
                }

                if (Player.Search.isSearching) Search(api, entity);

            }
            //Outside of loop
            if (findPlayer)
                PlayerFound(count);
        }        
        public static void ParseChat(EliteAPI api)
        {
            EliteAPI.ChatEntry c = api.Chat.GetNextChatLine();
            if (string.IsNullOrEmpty(c?.Text))
            {
                //Trigged our ChatLoaded bool if no new text is processed.
                if (!Structs.Chat.loaded) { Structs.Chat.loaded = true; Structs.Chat.SendEcho(api, Structs.Chat.loadStr); }
                return;
            }
            if (!Structs.Chat.loaded) return;

            const int party = Structs.Chat.Types.partyOut,
                echo = Structs.Chat.Types.echo;
            int chatType = c.ChatType;

            if (party == chatType)  ProcessParty(api, c.Text);
            else if (echo == chatType) ProcessEcho(api, c.Text);   
            
        }
        private static void ProcessParty(EliteAPI api, string text)
        {
            MatchCollection senderMatch = Regex.Matches(text, Structs.Chat.Warp.senderRegEx);
            MatchCollection coordMatch = Regex.Matches(text, Structs.Chat.Warp.coordRegEx);

            if (coordMatch.Count == Structs.Chat.Warp.expectedNumCoords)
                Player.PartyWarp(api, senderMatch, coordMatch);
        }
        private static void ProcessEcho(EliteAPI api, string text)
        { 
            MatchCollection echoMatch = Regex.Matches(text, Structs.Chat.Controller.echoRegex);
            if (echoMatch.Count == 1)
            {
                if (Structs.Chat.Controller.dictOneParam.ContainsKey(text))
                    Structs.Chat.Controller.dictOneParam[text](api);
                else return;
            }
            string firstMatch = echoMatch[0].ToString();
            switch (firstMatch)
            {
                case Structs.Chat.Controller.saveWarp:
                    SaveWarp(api, echoMatch);
                    break;
                case Structs.Chat.Controller.search:
                    Search(api, echoMatch);
                    break;
                case Structs.Chat.Controller.speed:
                    SharedFunctions.Speed(api, echoMatch[1].Value);
                    break;
                case Structs.Chat.Controller.select:
                    SharedFunctions.Select(api, echoMatch[1].Value);
                    break;
                case Structs.Chat.Controller.searchBG:
                    Search(echoMatch, Structs.URL.blueGartr);
                    break;
                case Structs.Chat.Controller.searchWiki:
                    Search(echoMatch, Structs.URL.wiki);
                    break;
            }
        }
        private static void SaveWarp(EliteAPI api, MatchCollection echoMatch)
        {
            string[] s = echoMatch.Cast<Match>()
                        .Select(m => m.Value)
                       .ToArray();

            string saveName = string.Join(" ", s.Skip(1));
            SharedFunctions.SaveWarp(api, saveName);
        }
        private static void Search(MatchCollection echoMatch, string url)
        {
            string[] s = echoMatch.Cast<Match>()
                            .Select(m => m.Value)
                            .ToArray();
            string term = string.Join(" ", s.Skip(1));
            OpenURL(url + term);
        }
        private static void Search(EliteAPI api, MatchCollection echoMatch)
        {
            string[] s = echoMatch.Cast<Match>()
                        .Select(m => m.Value)
                       .ToArray();

            string target = string.Join(" ", s.Skip(1));
            SharedFunctions.Search(api, target);
        }
        private static void Search(EliteAPI api, EliteAPI.XiEntity entity)
        {
            string target = Player.Search.target.ToLower();
            Console.WriteLine(entity.Name);
            //Found target
            if (entity.Name.ToLower().Contains(target))
            {
                Player.Search.isSearching = false;
                Player.Search.status = Structs.Search.success;
                Structs.Chat.SendEcho(api, Structs.Chat.Search.success);

                EliteAPI.TargetInfo t = api.Target.GetTargetInfo();
                if (t.TargetIndex != entity.TargetID)
                {
                    //Not targeted, so set target!
                    api.Target.SetTarget(Convert.ToInt32(entity.TargetID));
                }
                return;
            }
        }
        private static void OpenURL(string url)
        {
            System.Diagnostics.Process.Start(url);
        }
        private static void PlayerFound(int numPlayers)
        {
            if (numPlayers > 0) return;

            Updates.nearestPC.name = "";
            Updates.nearestPC.distance = 0;
            Player.isAlone = true;
        }
    }
}

