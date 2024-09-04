﻿using System;
using System.Linq;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;
using Lumina.Excel.GeneratedSheets2;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;

namespace AutoDuty.Helpers
{
    internal static class TrustHelper
    {
        internal static unsafe uint GetLevelFromTrustWindow(AtkUnitBase* addon)
        {
            if (addon == null)
                return 0;

            AtkComponentNode* atkResNode       = addon->GetComponentNodeById(88);
            AtkResNode*       resNode          = atkResNode->Component->UldManager.NodeList[5];
            AtkResNode*       resNodeChildNode = resNode->GetComponent()->UldManager.NodeList[0];
            return Convert.ToUInt32(resNodeChildNode->GetAsAtkCounterNode()->NodeText.ExtractText());
        }


        internal static bool CanTrustRun(this Content content, bool checkTrustLevels = true)
        {
            if (!content.TrustContent)
                return false;
            
            if (!UIState.IsInstanceContentCompleted(content.Id))
                return false;

            return !checkTrustLevels || CanTrustRunMembers(content);
        }

        private static bool CanTrustRunMembers(Content content)
        {
            if (content.TrustMembers.Any(tm => !tm.LevelIsSet)) 
                GetLevels(content);

            TrustMember?[] members = new TrustMember?[3];

            int index = 0;
            foreach (TrustMember member in content.TrustMembers)
            {
                if (member.Level >= content.ClassJobLevelRequired && members.CanSelectMember(member, (Player.Available ? Player.Object.GetRole() : CombatRole.NonCombat)))
                {
                    members[index++] = member;
                    if (index >= 3)
                        return true;
                }
            }

            return false;
        }

        internal static bool SetLevelingTrustMembers(Content content)
        {
            AutoDuty.Plugin.Configuration.SelectedTrustMembers = new TrustMemberName?[3];

            TrustMember?[] trustMembers = new TrustMember?[3];

            Job        playerJob  = Player.Available ? Player.Object.GetJob() : AutoDuty.Plugin.JobLastKnown;
            CombatRole playerRole = playerJob.GetRole();

            ObjectHelper.JobRole playerJobRole = Player.Available ? Player.Object.ClassJob.GameData?.GetJobRole() ?? ObjectHelper.JobRole.None : ObjectHelper.JobRole.None;

            Svc.Log.Info("Leveling Trust Members set");
            Svc.Log.Info(content.TrustMembers.Count.ToString());

            int index = 0;

            try
            {
                TrustMember[] membersPossible = content.TrustMembers
                                                       .OrderBy(tm => tm.Level + (tm.Level < tm.LevelCap ? 0 : 100) + 
                                                                      (playerRole == CombatRole.DPS ? playerJobRole == tm.Job.GetJobRole() ? 0.5f : 0 : 0)).ToArray();
                foreach (TrustMember member in membersPossible)
                {
                    Svc.Log.Info("checking: " + member.Name);

                    if (trustMembers.CanSelectMember(member, playerRole))
                    {
                        Svc.Log.Info("check successful");
                        trustMembers[index++] = member;

                        if (index >= 3)
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex.ToString());
            }

            if (trustMembers.All(tm => tm != null))
            {
                AutoDuty.Plugin.Configuration.SelectedTrustMembers = trustMembers.Select(tm => tm?.MemberName).ToArray();
                AutoDuty.Plugin.Configuration.Save();
                return true;
            }

            return false;
        }


        public static bool CanSelectMember(this TrustMember?[] trustMembers, TrustMember member, CombatRole playerRole) =>
            playerRole != CombatRole.NonCombat &&
            member.Role switch
            {
                TrustRole.DPS => playerRole == CombatRole.DPS && !trustMembers.Where(x => x != null).Any(x => x.Role is TrustRole.DPS) ||
                                 playerRole != CombatRole.DPS && trustMembers.Where(x => x  != null).Count(x => x.Role is TrustRole.DPS) < 2,
                TrustRole.Healer => playerRole != CombatRole.Healer && !trustMembers.Where(x => x != null).Any(x => x.Role is TrustRole.Healer),
                TrustRole.Tank => playerRole   != CombatRole.Tank   && !trustMembers.Where(x => x != null).Any(x => x.Role is TrustRole.Tank),
                TrustRole.AllRounder => true,
                _ => throw new ArgumentOutOfRangeException(member.Name, "member is of invalid role.. somehow")
            };
        internal static readonly Dictionary<TrustMemberName, TrustMember> Members = [];

        internal static void PopulateTrustMembers()
        {
            var dawnSheet = Svc.Data.GetExcelSheet<DawnMemberUIParam>();
            var jobSheet = Svc.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.ClassJob>();

            if (dawnSheet == null || jobSheet == null) return;

            void AddMember(TrustMemberName name, uint index, TrustRole role, ObjectHelper.ClassJobType classJob, uint levelInit = 71, uint levelCap = 100) => Members.Add(name, new TrustMember
            {
                Index = index,
                Name = dawnSheet.GetRow((uint)name)!.Unknown0.RawString,
                Role = role,
                Job = jobSheet.GetRow((uint)classJob)!,
                MemberName = name,
                LevelInit = levelInit,
                Level = levelInit,
                LevelCap = levelCap
            });

            AddMember(TrustMemberName.Alphinaud, 0, TrustRole.Healer, ObjectHelper.ClassJobType.Sage);
            AddMember(TrustMemberName.Alisaie, 1, TrustRole.DPS, ObjectHelper.ClassJobType.RedMage);
            AddMember(TrustMemberName.Thancred, 2, TrustRole.Tank, ObjectHelper.ClassJobType.Gunbreaker);
            AddMember(TrustMemberName.Urianger, 3, TrustRole.Healer, ObjectHelper.ClassJobType.Astrologian);
            AddMember(TrustMemberName.Yshtola, 4, TrustRole.DPS, ObjectHelper.ClassJobType.BlackMage);
            AddMember(TrustMemberName.Ryne, 5, TrustRole.DPS, ObjectHelper.ClassJobType.Rogue, 71, 80);
            AddMember(TrustMemberName.Estinien, 5, TrustRole.DPS, ObjectHelper.ClassJobType.Dragoon, 81);
            AddMember(TrustMemberName.Graha, 6, TrustRole.AllRounder, ObjectHelper.ClassJobType.BlackMage, 81);
            AddMember(TrustMemberName.Zero, 7, TrustRole.DPS, ObjectHelper.ClassJobType.Reaper, 90, 90);
            AddMember(TrustMemberName.Krile, 7, TrustRole.DPS, ObjectHelper.ClassJobType.Pictomancer, 91);
        }

        public static void ResetTrustIfInvalid()
        {
            if (AutoDuty.Plugin.Configuration.SelectedTrustMembers.Count(x => x is not null) == 3)
            {
                CombatRole playerRole = Player.Job.GetRole();

                TrustMember[] trustMembers = AutoDuty.Plugin.Configuration.SelectedTrustMembers.Select(name => Members[(TrustMemberName)name!]).ToArray();

                int dps = trustMembers.Count(x => x.Role is TrustRole.DPS);
                int healers = trustMembers.Count(x => x.Role is TrustRole.Healer);
                int tanks = trustMembers.Count(x => x.Role is TrustRole.Tank);

                bool needsReset = playerRole switch
                {
                    CombatRole.DPS => dps == 2,
                    CombatRole.Healer => healers == 1,
                    CombatRole.Tank => tanks == 1,
                    _ => false
                } || trustMembers.Any(tm => tm.Level < AutoDuty.Plugin.CurrentTerritoryContent?.ClassJobLevelRequired);

                if (needsReset)
                {
                    AutoDuty.Plugin.Configuration.SelectedTrustMembers = new TrustMemberName?[3];
                    AutoDuty.Plugin.Configuration.Save();
                }
            }
        }

        internal static void ClearCachedLevels() => Members.Each(x => x.Value.ResetLevel());

        internal static void ClearCachedLevels(Content content) => content.TrustMembers.Each(x => x.ResetLevel());

        internal static void GetLevels(Content? content)
        {
            if (State == ActionState.Running) return;
                    
            _getLevelsContent = content;

            _getLevelsContent ??= AutoDuty.Plugin.CurrentTerritoryContent;
            
            if (_getLevelsContent == null) return;

            if (_getLevelsContent.DawnIndex < 1)
                return;

            if (_getLevelsContent.TrustMembers.TrueForAll(tm => tm.LevelIsSet))
                return;

            if (!_getLevelsContent.CanTrustRun(false))
                return;

            Svc.Log.Info($"TrustHelper - Getting trust levels for expansion {_getLevelsContent.ExVersion}");

            State = ActionState.Running;
            
            Svc.Framework.Update += GetLevelsUpdate;
        }

        private unsafe static void Stop()
        {
            if (_getLevelsContent?.TrustMembers.TrueForAll(tm => tm.LevelIsSet) ?? false)
                AgentModule.Instance()->GetAgentByInternalId(AgentId.Dawn)->Hide();
            Svc.Framework.Update -= GetLevelsUpdate;
            State = ActionState.None; 
            Svc.Log.Info($"TrustHelper - Done getting trust levels for expansion {_getLevelsContent?.ExVersion}");
            _getLevelsContent = null;
        }

        internal static ActionState State = ActionState.None;

        private static Content? _getLevelsContent = null;
        internal static unsafe void GetLevelsUpdate(IFramework framework)
        {
            if (_getLevelsContent == null || AutoDuty.Plugin.InDungeon)
                Stop();

            if (!EzThrottler.Throttle("GetLevelsUpdate", 5) || !ObjectHelper.IsValid) return;

            if (!GenericHelpers.TryGetAddonByName("Dawn", out AtkUnitBase* addonDawn) || !GenericHelpers.IsAddonReady(addonDawn))
            {
                if (EzThrottler.Throttle("OpenDawn", 5000))
                {
                    Svc.Log.Debug("TrustHelper Helper - Opening Dawn");
                    AgentModule.Instance()->GetAgentByInternalId(AgentId.Dawn)->Show();
                }
                return;
            }
            else
                EzThrottler.Throttle("OpenDawn", 5, true);

            if (addonDawn->AtkValues[225].UInt < (_getLevelsContent!.ExVersion - 2))
            {
                Svc.Log.Debug($"TrustHelper Helper - You do not have expansion: {_getLevelsContent.ExVersion} unlocked stopping");
                Stop();
                return;
            }

            if (addonDawn->AtkValues[226].UInt != (_getLevelsContent!.ExVersion - 3))
            {
                Svc.Log.Debug($"TrustHelper Helper - Opening Expansion: {_getLevelsContent.ExVersion}");
                AddonHelper.FireCallBack(addonDawn, true, 20, (_getLevelsContent!.ExVersion - 3));
            }
            else if (addonDawn->AtkValues[151].UInt != _getLevelsContent.DawnIndex)
            {
                Svc.Log.Debug($"TrustHelper Helper - Clicking: {_getLevelsContent.EnglishName} at index: {_getLevelsContent.DawnIndex}");
                AddonHelper.FireCallBack(addonDawn, true, 15, _getLevelsContent.DawnIndex);
            }
            else
            {
                for (int id = 0; id < _getLevelsContent.TrustMembers.Count; id++)
                {
                    int index = id;

                    if (!_getLevelsContent.TrustMembers[index].LevelIsSet)
                    {
                        
                        AddonHelper.FireCallBack(addonDawn, true, 16, index);
                        var lvl = GetLevelFromTrustWindow(addonDawn);
                        Svc.Log.Debug($"TrustHelper - Setting {_getLevelsContent.TrustMembers[index].MemberName} level to {lvl}");
                        _getLevelsContent.TrustMembers[index].SetLevel(lvl);
                    }
                }
                Stop();
            }
        }
    }
}
