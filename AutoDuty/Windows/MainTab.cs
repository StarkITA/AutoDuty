﻿using AutoDuty.Helpers;
using AutoDuty.IPC;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using ECommons.ImGuiMethods;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static AutoDuty.AutoDuty;

namespace AutoDuty.Windows
{
    internal static class MainTab
    {
        internal static ContentPathsManager.ContentPathContainer? DutySelected;
        internal static readonly (string Normal, string GameFont) Digits = ("0123456789", "");

        private static int _currentStepIndex = -1;
        private static readonly string _pathsURL = "https://github.com/ffxivcode/AutoDuty/tree/master/AutoDuty/Paths";

        internal static void Draw()
        {
            if (MainWindow.CurrentTabName != "Main")
                MainWindow.CurrentTabName = "Main";
            var dutyMode = Plugin.Configuration.DutyModeEnum;
            var levelingMode = Plugin.LevelingModeEnum;

            static void DrawPathSelection()
            {
                if (Plugin.CurrentTerritoryContent == null || !ObjectHelper.IsReady)
                    return;

                using var d = ImRaii.Disabled(Plugin is { InDungeon: true, Stage: > 0 });

                if (ContentPathsManager.DictionaryPaths.TryGetValue(Plugin.CurrentTerritoryContent.TerritoryType, out var container))
                {
                    List<ContentPathsManager.DutyPath> curPaths = container.Paths;
                    if (curPaths.Count > 1)
                    {
                        int curPath = Math.Clamp(Plugin.CurrentPath, 0, curPaths.Count - 1);
                        using (ImRaii.Disabled(!Plugin.Configuration.PathSelections.ContainsKey(Plugin.CurrentTerritoryContent.TerritoryType) || !Plugin.Configuration.PathSelections[Plugin.CurrentTerritoryContent.TerritoryType].ContainsKey(Svc.ClientState.LocalPlayer.GetJob())))
                        {
                            if (ImGui.Button("Clear Saved Path"))
                            {
                                Plugin.Configuration.PathSelections[Plugin.CurrentTerritoryContent.TerritoryType].Remove(Svc.ClientState.LocalPlayer.GetJob());
                                Plugin.Configuration.Save();
                                if (!Plugin.InDungeon)
                                    container.SelectPath(out Plugin.CurrentPath);
                            }
                        }
                        ImGui.SameLine();
                        ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
                        if (ImGui.Combo("##SelectedPath", ref curPath, [.. curPaths.Select(dp => dp.Name)], curPaths.Count))
                        {
                            if (!Plugin.Configuration.PathSelections.ContainsKey(Plugin.CurrentTerritoryContent!.TerritoryType))
                                Plugin.Configuration.PathSelections.Add(Plugin.CurrentTerritoryContent.TerritoryType, []);

                            Plugin.Configuration.PathSelections[Plugin.CurrentTerritoryContent.TerritoryType][Svc.ClientState.LocalPlayer.GetJob()] = curPath;
                            Plugin.Configuration.Save();
                            Plugin.CurrentPath = curPath;
                            Plugin.LoadPath();
                        }
                        ImGui.PopItemWidth();
                    }
                }
            }

            if (Plugin.InDungeon)
            {
                if (Plugin.CurrentTerritoryContent == null)
                    Plugin.LoadPath();
                else
                {
                    var progress = VNavmesh_IPCSubscriber.IsEnabled ? VNavmesh_IPCSubscriber.Nav_BuildProgress() : 0;
                    if (progress >= 0)
                    {
                        ImGui.Text($"{Plugin.CurrentTerritoryContent.Name} Mesh: Loading: ");
                        ImGui.ProgressBar(progress, new(200, 0));
                    }
                    else
                        ImGui.Text($"{Plugin.CurrentTerritoryContent.Name} Mesh: Loaded Path: {(ContentPathsManager.DictionaryPaths.ContainsKey(Plugin.CurrentTerritoryContent.TerritoryType) ? "Loaded" : "None")}");

                    ImGui.Separator();
                    ImGui.Spacing();

                    DrawPathSelection();
                    if (!Plugin.States.HasFlag(PluginState.Looping) && !Plugin.Overlay.IsOpen)
                        MainWindow.GotoAndActions();
                    using (ImRaii.Disabled(!VNavmesh_IPCSubscriber.IsEnabled || !Plugin.InDungeon || !VNavmesh_IPCSubscriber.Nav_IsReady() || !BossMod_IPCSubscriber.IsEnabled))
                    {
                        using (ImRaii.Disabled(!Plugin.InDungeon || !ContentPathsManager.DictionaryPaths.ContainsKey(Plugin.CurrentTerritoryContent.TerritoryType)))
                        {
                            if (Plugin.Stage == 0)
                            {
                                if (ImGui.Button("Start"))
                                {
                                    Plugin.LoadPath();
                                    _currentStepIndex = -1;
                                    if (Plugin.MainListClicked)
                                        Plugin.StartNavigation(!Plugin.MainListClicked);
                                    else
                                        Plugin.Run(Svc.ClientState.TerritoryType);
                                }
                            }
                            else
                                MainWindow.StopResumePause();
                            ImGui.SameLine(0, 15);
                        }
                        ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
                        MainWindow.LoopsConfig();
                        ImGui.PopItemWidth();

                        if (!ImGui.BeginListBox("##MainList", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y))) return;

                        if ((VNavmesh_IPCSubscriber.IsEnabled || Plugin.Configuration.UsingAlternativeMovementPlugin) && (BossMod_IPCSubscriber.IsEnabled || Plugin.Configuration.UsingAlternativeBossPlugin) && (ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled || BossMod_IPCSubscriber.IsEnabled  || Plugin.Configuration.UsingAlternativeRotationPlugin))
                        {
                            foreach (var item in Plugin.ListBoxPOSText.Select((name, index) => (name, index)))
                            {
                                var v4 = item.index == Plugin.Indexer ? new Vector4(0, 255, 255, 1) : (item.name.StartsWith("<--", StringComparison.InvariantCultureIgnoreCase) ? new Vector4(0, 255, 0, 1) : new Vector4(255, 255, 255, 1));
                                ImGui.TextColored(v4, item.name);
                                if (ImGui.IsItemClicked(ImGuiMouseButton.Left) && Plugin.Stage == 0)
                                {
                                    if (item.index == Plugin.Indexer)
                                    {
                                        Plugin.Indexer = -1;
                                        Plugin.MainListClicked = false;
                                    }
                                    else
                                    {
                                        Plugin.Indexer = item.index;
                                        Plugin.MainListClicked = true;
                                    }
                                }
                            }
                            if (_currentStepIndex != Plugin.Indexer && _currentStepIndex > -1 && Plugin.Stage > 0)
                            {
                                var lineHeight = ImGui.GetTextLineHeightWithSpacing();
                                _currentStepIndex = Plugin.Indexer;
                                if (_currentStepIndex > 1)
                                    ImGui.SetScrollY((_currentStepIndex - 1) * lineHeight);
                            }
                            else if (_currentStepIndex == -1 && Plugin.Stage > 0)
                            {
                                _currentStepIndex = 0;
                                ImGui.SetScrollY(_currentStepIndex);
                            }
                            if (Plugin.InDungeon && Plugin.ListBoxPOSText.Count < 1 && !ContentPathsManager.DictionaryPaths.ContainsKey(Plugin.CurrentTerritoryContent.TerritoryType))
                                ImGui.TextColored(new Vector4(0, 255, 0, 1), $"No Path file was found for:\n{TerritoryName.GetTerritoryName(Plugin.CurrentTerritoryContent.TerritoryType).Split('|')[1].Trim()}\n({Plugin.CurrentTerritoryContent.TerritoryType}.json)\nin the Paths Folder:\n{Plugin.PathsDirectory.FullName.Replace('\\', '/')}\nPlease download from:\n{_pathsURL}\nor Create in the Build Tab");
                        }
                        else
                        {
                            if (!VNavmesh_IPCSubscriber.IsEnabled && !Plugin.Configuration.UsingAlternativeMovementPlugin)
                                ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires VNavmesh plugin to be Installed and Loaded\nPlease add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                            if (!BossMod_IPCSubscriber.IsEnabled && !Plugin.Configuration.UsingAlternativeBossPlugin)
                                ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires BossMod plugin to be Installed and Loaded\nPlease add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                            if (!ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled && !BossMod_IPCSubscriber.IsEnabled && !Plugin.Configuration.UsingAlternativeRotationPlugin)
                                ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires a Rotation plugin to be Installed and Loaded (Either Rotation Solver Reborn or BossMod AutoRotation)");
                        }
                        ImGui.EndListBox();
                    }
                }
            }
            else
            {
                if (!Plugin.States.HasFlag(PluginState.Looping) && !Plugin.Overlay.IsOpen)
                    MainWindow.GotoAndActions();

                using (ImRaii.Disabled(Plugin.CurrentTerritoryContent == null || (Plugin.Configuration.DutyModeEnum == DutyMode.Trust && Plugin.Configuration.SelectedTrustMembers.Any(x => x is null))))
                {
                    if (!Plugin.States.HasFlag(PluginState.Looping))
                    {
                        if (ImGui.Button("Run"))
                        {
                            if (Plugin.Configuration.DutyModeEnum == DutyMode.None)
                                MainWindow.ShowPopup("Error", "You must select a version\nof the dungeon to run");
                            else if (Svc.Party.PartyId > 0 && (Plugin.Configuration.DutyModeEnum == DutyMode.Support || Plugin.Configuration.DutyModeEnum == DutyMode.Squadron || Plugin.Configuration.DutyModeEnum == DutyMode.Trust))
                                MainWindow.ShowPopup("Error", "You must not be in a party to run Support, Squadron or Trust");
                            else if (Plugin.Configuration.DutyModeEnum == DutyMode.Regular && !Plugin.Configuration.Unsynced && !Plugin.Configuration.OverridePartyValidation && Svc.Party.PartyId == 0)
                                MainWindow.ShowPopup("Error", "You must be in a group of 4 to run Regular Duties");
                            else if (Plugin.Configuration.DutyModeEnum == DutyMode.Regular && !Plugin.Configuration.Unsynced && !Plugin.Configuration.OverridePartyValidation && !ObjectHelper.PartyValidation())
                                MainWindow.ShowPopup("Error", "You must have the correct party makeup to run Regular Duties");
                            else if (ContentPathsManager.DictionaryPaths.ContainsKey(Plugin.CurrentTerritoryContent?.TerritoryType ?? 0))
                                Plugin.Run();
                            else
                                MainWindow.ShowPopup("Error", "No path was found");
                        }
                    }
                    else
                        MainWindow.StopResumePause();
                }
                using (ImRaii.Disabled(Plugin.States.HasFlag(PluginState.Looping)))
                {
                    using (ImRaii.Disabled(Plugin.CurrentTerritoryContent == null))
                    {
                        ImGui.SameLine(0, 15);
                        ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
                        MainWindow.LoopsConfig();
                        ImGui.PopItemWidth();
                    }
                    ImGui.TextColored(Plugin.Configuration.DutyModeEnum == DutyMode.None ? new Vector4(1, 0, 0, 1) : new Vector4(0, 1, 0, 1), "Select Duty Mode: ");
                    ImGui.SameLine(0);
                    ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
                    if (ImGui.BeginCombo("##DutyModeEnum", EnumString(Plugin.Configuration.DutyModeEnum)))
                    {
                        foreach (DutyMode mode in Enum.GetValues(typeof(DutyMode)))
                        {
                            if (ImGui.Selectable(EnumString(mode)))
                            {
                                Plugin.Configuration.DutyModeEnum = mode;
                                Plugin.Configuration.Save();
                            }
                        }
                        ImGui.EndCombo();
                    }
                    ImGui.PopItemWidth();
                    if (Plugin.Configuration.DutyModeEnum != DutyMode.None)
                    {
                        if (Plugin.Configuration.DutyModeEnum == DutyMode.Support || Plugin.Configuration.DutyModeEnum == DutyMode.Trust)
                        {
                            ImGui.TextColored(Plugin.LevelingModeEnum == LevelingMode.None ? new Vector4(1, 0, 0, 1) : new Vector4(0, 1, 0, 1), "Select Leveling Mode: ");
                            ImGui.SameLine(0);
                            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
                            if (ImGui.BeginCombo("##LevelingModeEnum", Plugin.LevelingModeEnum == LevelingMode.None ? "None" : "Auto"))
                            {
                                if (ImGui.Selectable("None"))
                                {
                                    Plugin.LevelingModeEnum = LevelingMode.None;
                                    Plugin.Configuration.Save();
                                }
                                if (ImGui.Selectable("Auto"))
                                {
                                    Plugin.LevelingModeEnum = Plugin.Configuration.DutyModeEnum == DutyMode.Support ? LevelingMode.Support : LevelingMode.Trust;
                                    Plugin.Configuration.Save();
                                    if (Plugin.Configuration.AutoEquipRecommendedGear)
                                        AutoEquipHelper.Invoke();
                                }
                                ImGui.EndCombo();
                            }
                            ImGui.PopItemWidth();
                            
                            bool equip = Plugin.Configuration.AutoEquipRecommendedGear;

                            if (Plugin.Configuration.DutyModeEnum != DutyMode.Trust) ImGuiComponents.HelpMarker("Leveling Mode will queue you for the most CONSISTENT dungeon considering your lvl + Ilvl. \nIt will NOT always queue you for the highest level dungeon, it follows our stable dungeon list instead.");
                            else ImGuiComponents.HelpMarker("TRUST Leveling Mode will queue you for the most CONSISTENT dungeon considering your lvl + Ilvl, as well as the LOWEST LEVEL trust members you have, in an attempt to level them all equally.\nIt will NOT always queue you for the highest level dungeon, it follows our stable dungeon list instead.");
                        }

                        if (Plugin.Configuration.DutyModeEnum == DutyMode.Trust && Player.Available)
                        {
                            ImGui.Separator();
                            if (DutySelected != null && DutySelected.Content.TrustMembers.Count > 0)
                            {
                                ImGuiEx.LineCentered(() => ImGuiEx.TextUnderlined("Select your Trust Party"));
                                ImGui.Columns(3, null, false);

                                TrustHelper.ResetTrustIfInvalid();
                                for (int i = 0; i < Plugin.Configuration.SelectedTrustMembers.Length; i++)
                                {
                                    TrustMemberName? member = Plugin.Configuration.SelectedTrustMembers[i];

                                    if (member is null)
                                        continue;

                                    if (DutySelected.Content.TrustMembers.All(x => x.MemberName != member))
                                    {
                                        Svc.Log.Debug($"Killing {member}");
                                        Plugin.Configuration.SelectedTrustMembers[i] = null;
                                    }
                                }

                                using (ImRaii.Disabled(Plugin.TrustLevelingEnabled && TrustHelper.Members.Any(tm => tm.Value.Level < tm.Value.LevelCap)))
                                {
                                    foreach (TrustMember member in DutySelected.Content.TrustMembers)
                                    {
                                        bool enabled = Plugin.Configuration.SelectedTrustMembers.Where(x => x != null).Any(x => x == member.MemberName);
                                        CombatRole playerRole = Player.Job.GetRole();
                                        int numberSelected = Plugin.Configuration.SelectedTrustMembers.Count(x => x != null);

                                        TrustMember?[] members = Plugin.Configuration.SelectedTrustMembers.Select(tmn => tmn != null ? TrustHelper.Members[(TrustMemberName)tmn] : null).ToArray();

                                        bool canSelect = members.CanSelectMember(member, playerRole) && member.Level >= DutySelected.Content.ClassJobLevelRequired;

                                        using (ImRaii.Disabled(!enabled && (numberSelected == 3 || !canSelect)))
                                        {
                                            if (ImGui.Checkbox($"###{member.Index}{DutySelected.id}", ref enabled))
                                            {
                                                if (enabled)
                                                {
                                                    for (int i = 0; i < 3; i++)
                                                    {
                                                        if (Plugin.Configuration.SelectedTrustMembers[i] is null)
                                                        {
                                                            Plugin.Configuration.SelectedTrustMembers[i] = member.MemberName;
                                                            break;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    if (Plugin.Configuration.SelectedTrustMembers.Where(x => x != null).Any(x => x == member.MemberName))
                                                    {
                                                        int idx = Plugin.Configuration.SelectedTrustMembers.IndexOf(x => x != null && x == member.MemberName);
                                                        Plugin.Configuration.SelectedTrustMembers[idx] = null;
                                                    }
                                                }

                                                Plugin.Configuration.Save();
                                            }
                                        }

                                        ImGui.SameLine(0, 2);
                                        ImGui.SetItemAllowOverlap();
                                        ImGui.TextColored(member.Role switch
                                        {
                                            TrustRole.DPS => ImGuiHelper.RoleDPSColor,
                                            TrustRole.Healer => ImGuiHelper.RoleHealerColor,
                                            TrustRole.Tank => ImGuiHelper.RoleTankColor,
                                            TrustRole.AllRounder => ImGuiHelper.RoleAllRounderColor,
                                            _ => Vector4.One
                                        }, member.Name);
                                        if (member.Level > 0)
                                        {
                                            ImGui.SameLine(0, 2);
                                            ImGuiEx.TextV(member.Level < member.LevelCap ? ImGuiHelper.White : ImGuiHelper.MaxLevelColor, $"{member.Level.ToString().ReplaceByChar(Digits.Normal, Digits.GameFont)}");
                                        }

                                        ImGui.NextColumn();
                                    }
                                }

                                if (DutySelected.Content.TrustMembers.Count == 7)
                                    ImGui.NextColumn();

                                if (ImGui.Button("Refresh", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
                                {
                                    if (InventoryHelper.CurrentItemLevel < 370)
                                        AutoDuty.Plugin.LevelingModeEnum = LevelingMode.None;
                                    TrustHelper.ClearCachedLevels();
                                }
                                ImGui.NextColumn();
                                ImGui.Columns(1, null, true);
                            } else if (ImGui.Button("Refresh trust member levels"))
                            {
                                if (InventoryHelper.CurrentItemLevel < 370)
                                    AutoDuty.Plugin.LevelingModeEnum = LevelingMode.None;
                                TrustHelper.ClearCachedLevels();
                            }
                        }

                        DrawPathSelection();
                        ImGui.Separator();
                        if (ImGui.Checkbox("Hide Unavailable Duties", ref Plugin.Configuration.HideUnavailableDuties))
                            Plugin.Configuration.Save();
                        if (Plugin.Configuration.DutyModeEnum == DutyMode.Regular || Plugin.Configuration.DutyModeEnum == DutyMode.Trial || Plugin.Configuration.DutyModeEnum == DutyMode.Raid)
                        {
                            if (ImGuiEx.CheckboxWrapped("Unsynced", ref Plugin.Configuration.Unsynced))
                                Plugin.Configuration.Save();
                        }
                    }
                    var ilvl = InventoryHelper.CurrentItemLevel;
                    if (!ImGui.BeginListBox("##DutyList", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y))) return;

                    if (VNavmesh_IPCSubscriber.IsEnabled && BossMod_IPCSubscriber.IsEnabled)
                    {
                        if (ObjectHelper.IsReady)
                        {
                            if (Plugin.LevelingModeEnum != LevelingMode.None)
                            {
                                if (Player.Job.GetRole() == CombatRole.NonCombat || (Plugin.LevelingModeEnum == LevelingMode.Trust && ilvl < 370) || (Plugin.LevelingModeEnum == LevelingMode.Trust && Plugin.CurrentPlayerItemLevelandClassJob.Value != null && Plugin.CurrentPlayerItemLevelandClassJob.Value != Player.Job))
                                {
                                    Svc.Log.Debug($"You are on a non-compatible job: {Player.Job.GetRole()}, or your doing trust and your iLvl({ilvl}) is below 370, or your iLvl has changed, Disabling Leveling Mode");
                                    Plugin.LevelingModeEnum = LevelingMode.None;
                                }
                                else if (ilvl > 0 && ilvl != Plugin.CurrentPlayerItemLevelandClassJob.Key)
                                {
                                    Svc.Log.Debug($"Your iLvl has changed, Selecting new Duty.");
                                    Plugin.CurrentTerritoryContent = LevelingHelper.SelectHighestLevelingRelevantDuty(Plugin.LevelingModeEnum == LevelingMode.Trust);
                                }
                                else
                                {
                                    ImGuiEx.TextWrapped(new Vector4(0, 1, 0, 1), $"Leveling Mode: L{Player.Level} (i{ilvl})");
                                    foreach (var item in LevelingHelper.LevelingDuties.Select((Value, Index) => (Value, Index)))
                                    {
                                        if (Plugin.Configuration.DutyModeEnum == DutyMode.Trust && !item.Value.TrustContent)
                                            continue;
                                        var disabled = !item.Value.CanRun() || (Plugin.Configuration.DutyModeEnum == DutyMode.Trust && !item.Value.CanTrustRun(true));
                                        if (!Plugin.Configuration.HideUnavailableDuties || !disabled)
                                        {
                                            using (ImRaii.Disabled(disabled))
                                            {
                                                ImGuiEx.TextWrapped(item.Value == Plugin.CurrentTerritoryContent ? new Vector4(0, 1, 1, 1) : new Vector4(1, 1, 1, 1), $"L{item.Value.ClassJobLevelRequired} (i{item.Value.ItemLevelRequired}): {item.Value.EnglishName}");
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (Player.Job.GetRole() == CombatRole.NonCombat)
                                    ImGuiEx.TextWrapped(new Vector4(255, 1, 0, 1), "Please switch to a combat job to use AutoDuty.");

                                if ((Player.Job.GetRole() != CombatRole.NonCombat && Player.Job != Job.BLU) || (Player.Job == Job.BLU && (Plugin.Configuration.DutyModeEnum == DutyMode.Regular || Plugin.Configuration.DutyModeEnum == DutyMode.Trial || Plugin.Configuration.DutyModeEnum == DutyMode.Raid)))
                                {
                                    Dictionary<uint, Content> dictionary = [];
                                    if (Plugin.Configuration.DutyModeEnum == DutyMode.Support)
                                        dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.DawnContent).ToDictionary();
                                    else if (Plugin.Configuration.DutyModeEnum == DutyMode.Trust)
                                        dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.TrustContent).ToDictionary();
                                    else if (Plugin.Configuration.DutyModeEnum == DutyMode.Squadron)
                                        dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.GCArmyContent).ToDictionary();
                                    else if (Plugin.Configuration.DutyModeEnum == DutyMode.Regular)
                                        dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.ContentType == 2).ToDictionary();
                                    else if (Plugin.Configuration.DutyModeEnum == DutyMode.Trial)
                                        dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.ContentType == 4).ToDictionary();
                                    else if (Plugin.Configuration.DutyModeEnum == DutyMode.Raid)
                                        dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.ContentType == 5).ToDictionary();
                                    else if (Plugin.Configuration.DutyModeEnum == DutyMode.Variant)
                                        dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.VariantContent).ToDictionary();

                                    if (dictionary.Count > 0 && ObjectHelper.IsReady)
                                    {
                                        short level = PlayerHelper.GetCurrentLevelFromSheet();
                                        foreach ((uint _, Content? content) in dictionary)
                                        {
                                            bool canRun = content.CanRun(level) && (Plugin.Configuration.DutyModeEnum != DutyMode.Trust || content.CanTrustRun());
                                            using (ImRaii.Disabled(!canRun))
                                            {
                                                if (Plugin.Configuration.HideUnavailableDuties && !canRun)
                                                    continue;
                                                if (ImGui.Selectable($"({content.TerritoryType}) {content.Name}", DutySelected?.id == content.TerritoryType))
                                                {
                                                    DutySelected = ContentPathsManager.DictionaryPaths[content.TerritoryType];
                                                    Plugin.CurrentTerritoryContent = content;
                                                    DutySelected.SelectPath(out Plugin.CurrentPath);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (ObjectHelper.IsReady)
                                            ImGuiEx.TextWrapped(new Vector4(0, 1, 0, 1), "Please select one of Support, Trust, Squadron or Regular\nto Populate the Duty List");
                                    }
                                }
                            }
                        }
                        else if (Player.Job == Job.BLU)
                            ImGuiEx.TextWrapped(new Vector4(0, 1, 1, 1), "Blue Mage cannot run Trust, Duty Support, Squadron or Variant dungeons. Please switch jobs or select a different category.");
                        else
                            ImGuiEx.TextWrapped(new Vector4(0, 1, 0, 1), "Busy...");
                    }
                    else
                    {
                        if (!VNavmesh_IPCSubscriber.IsEnabled)
                            ImGuiEx.TextWrapped(new Vector4(255, 0, 0, 1), "AutoDuty requires vnavmesh plugin to be installed and loaded for proper navigation and movement. Please add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                        if (!BossMod_IPCSubscriber.IsEnabled)
                            ImGuiEx.TextWrapped(new Vector4(255, 0, 0, 1), "AutoDuty requires BossMod plugin to be installed and loaded for proper mechanic handling. Please add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                    }
                    ImGui.EndListBox();
                }
            }
        }

        internal static void PathsUpdated()
        {
            DutySelected = null;
        }
    }
}
