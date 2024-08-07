using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements.Village;
using ExileCore.PoEMemory.FilesInMemory.Village;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using SharpDX;
using Vector2 = System.Numerics.Vector2;

namespace VillageHelper;

public class VillageHelper : BaseSettingsPlugin<VillageHelperSettings>
{
    private static readonly Dictionary<string, float> AverageWorkerWages = new Dictionary<string, float>()
    {
        ["Mining"] = 5.5f,
        ["Smelting"] = 6.5f,
        ["Farming"] = 13.08f,
        ["Disenchanting"] = 20.66f,
        ["Shipping"] = 7.64f,
        ["Mapping"] = 35.7f,
    };
    
    private static readonly Dictionary<string, float> ResourceValues = new Dictionary<string, float>()
    {
        ["Crimson Iron"] = 16f,
        ["Orichalcum"] = 19f,
        ["Petrified Amber"] = 24f,
        ["Bismuth"] = 37f,
        ["Verisium"] = 64f,
        ["Wheat"] = 12f,
        ["Corn"] = 15f,
        ["Pumpkin"] = 18f,
        ["Orgourd"] = 21f,
        ["Blue Zanthimum"] = 24f,
    };

    public override bool Initialise()
    {
        GameController.LeftPanel.WantUse(() => true);
        Settings.ExportWorkerStats.OnPressed += () =>
        {
            var text = "";
            foreach (var jobType in GameController.Game.Files.VillageJobTypes.EntriesList)
            {
                var workers = GameController.IngameState.IngameUi.VillageScreen.Workers
                    .Where(x => x.JobRanks.Count == 1 && x.JobRanks.Single().Key.Equals(jobType) ||
                                x.JobRanks.OrderByDescending(x => x.Value).Take(2).ToList() switch
                                {
                                    var l => l[0].Key.Equals(jobType) && l[0].Value - l[1].Value >= 2,
                                }).ToList();
                text +=
                    $"{jobType.Id} {{{string.Join(",", workers.Select(w => $"{{{GameController.Game.Files.VillageJobSkillLevels.GetByLevel(w.JobRanks[jobType]).Speed},{w.WagePerHour}}}"))}}}\n";
            }

            ImGui.SetClipboardText(text);
        };
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
    }

    public override Job Tick()
    {
        return null;
    }

    public override void Render()
    {
        if (Settings.ToggleWindowHotkey.PressedOnce())
        {
            Settings.WindowShown = !Settings.WindowShown;
        }

        var villageScreen = GameController.IngameState.IngameUi.VillageScreen;
        var zoneLoadResources = villageScreen.ZoneLoadResources;
        var villageGold = Settings.ShowProjectedCurrentGold ? villageScreen.CurrentGold : zoneLoadResources.Gold;
        if (Settings.WindowShown &&
            zoneLoadResources.Resources is { Count: > 0 } resources &&
            ImGui.Begin("Village stats"))
        {
            if (Settings.ShowUpgrades &&
                ImGui.TreeNodeEx("Upgrades", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (GameController.Player?.GetComponent<Player>() is { } player)
                {
                    var nextUpgradeTiers = player.VillageUpgrades.Where(x => !x.Value).GroupBy(x => x.Key.Category.Id).Select(x => x.MinBy(y => y.Key.Tier)).ToList();
                    foreach (var nextUpgradeTier in nextUpgradeTiers)
                    {
                        ImGui.Text($"{nextUpgradeTier.Key}");
                        ImGui.SameLine();
                        foreach (var requiredResource in nextUpgradeTier.Key.RequiredResources)
                        {
                            var enoughResource = requiredResource.Item2 <= resources.GetValueOrDefault(requiredResource.Item1);
                            ImGui.TextColored(enoughResource ? Settings.GoodColor.Value.ToImguiVec4() : Settings.BadColor.Value.ToImguiVec4(),
                                $"{requiredResource.Item2}x {requiredResource.Item1.Export?.Name ?? requiredResource.Item1.Id},");
                            ImGui.SameLine();
                        }

                        var enoughGold = nextUpgradeTier.Key.RequiredGold <= villageGold;
                        ImGui.TextColored(enoughGold ? Settings.GoodColor.Value.ToImguiVec4() : Settings.BadColor.Value.ToImguiVec4(),
                            $"{nextUpgradeTier.Key.RequiredGold} gold");
                    }
                }

                ImGui.TreePop();
            }

            if (Settings.ShowResources &&
                ImGui.TreeNodeEx("Resources", ImGuiTreeNodeFlags.DefaultOpen))
            {
                foreach (var resource in resources)
                {
                    if (resource.Value == 0 && !Settings.ShowEmptyResources)
                    {
                        continue;
                    }

                    var colorSet = resource.Value == 0 && (Settings.ShowEmptyResourcesInColor);
                    if (colorSet)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, Settings.BadColor.Value.ToImguiVec4());
                    }

                    ImGui.Text($"{resource.Value}x {resource.Key.Export?.Name ?? resource.Key.Id}");
                    if (colorSet)
                    {
                        ImGui.PopStyleColor();
                    }
                }

                {
                    var colorSet = villageGold <= 1 && (Settings.ShowEmptyResourcesInColor);
                    if (colorSet)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, Settings.BadColor.Value.ToImguiVec4());
                    }

                    ImGui.Text($"{villageGold} gold (-{villageScreen.TotalWagePerHour}/hr)");
                    if (colorSet)
                    {
                        ImGui.PopStyleColor();
                    }
                }
                ImGui.TreePop();
            }

            if (Settings.ShowActions &&
                ImGui.TreeNodeEx("Actions", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (villageScreen.RemainingShipmentTimes is { Count: > 0 })
                {
                    foreach (var remainingShipmentTime in villageScreen.RemainingShipmentTimes)
                    {
                        if (remainingShipmentTime <= TimeSpan.Zero)
                        {
                            ImGui.TextColored(Settings.GoodColor.Value.ToImguiVec4(), "Shipment arrived!");
                        }
                        else
                        {
                            ImGui.Text($"Shipment arrives in {remainingShipmentTime:hh\\:mm\\:ss}");
                        }
                    }
                }

                if (villageScreen.RemainingDisenchantmentTime <= TimeSpan.Zero)
                {
                    ImGui.TextColored(Settings.BadColor.Value.ToImguiVec4(), "Nothing to disenchant");
                }
                else
                {
                    ImGui.Text($"Disenchantment completes in {villageScreen.RemainingDisenchantmentTime:hh\\:mm\\:ss}");
                }

                ImGui.TreePop();
            }
        }

        if (Settings.ShowStatusOverlay)
        {
            var position = GameController.LeftPanel.StartDrawPointNum;
            var shipmentTextSize = new[]
            {
                Graphics.MeasureText("SHIPMENT 00:00!"),
                Graphics.MeasureText("DISENCHANTMENT EMPTY!"),
                Graphics.MeasureText("99999 gold (-99999/hr) 00:00"),
            }.MaxBy(x => x.X);
            var positionLeft = position - shipmentTextSize with { Y = 0 };
            positionLeft.X += Settings.StatusOverlayXOffset;
            positionLeft.Y += Settings.StatusOverlayYOffset;

            if (Settings.ShowActions)
            {
                if (villageScreen.RemainingShipmentTimes is { Count: > 0 })
                {
                    foreach (var remainingShipmentTime in villageScreen.RemainingShipmentTimes)
                    {
                        var drawText = remainingShipmentTime <= TimeSpan.Zero
                            ? Graphics.DrawTextWithBackground("SHIPMENT HERE!", positionLeft, Settings.GoodColor, Color.Black)
                            : Graphics.DrawTextWithBackground($"Shipment {remainingShipmentTime:hh\\:mm}", positionLeft, Settings.NeutralColor, Color.Black);
                        position.Y += drawText.Y;
                        positionLeft.Y += drawText.Y;
                    }
                }

                var textSize = villageScreen.RemainingDisenchantmentTime <= TimeSpan.Zero
                    ? Graphics.DrawTextWithBackground("DISENCHANTMENT EMPTY!", positionLeft, Settings.BadColor, Color.Black)
                    : Graphics.DrawTextWithBackground($"Disenchantment {villageScreen.RemainingDisenchantmentTime:hh\\:mm}", positionLeft, Settings.NeutralColor, Color.Black);
                position.Y += textSize.Y;
                positionLeft.Y += textSize.Y;
            }

            if (Settings.ShowResources)
            {
                var colorSet = villageGold <= 1 && Settings.ShowEmptyResourcesInColor;
                var textSize = Graphics.DrawTextWithBackground(
                    $"{villageGold} gold (-{villageScreen.TotalWagePerHour}/hr) {TimeSpan.FromHours(villageGold / (float)villageScreen.TotalWagePerHour):hh\\:mm}", positionLeft,
                    colorSet ? Settings.BadColor : Settings.NeutralColor, Color.Black);

                position.Y += textSize.Y;
                positionLeft.Y += textSize.Y;
            }

            GameController.LeftPanel.StartDrawPointNum = position;
        }

        if (Settings.ShowWorkerUpgradeTips)
        {
            HashSet<string> workersToReplace = [];
            if (GameController.IngameState.IngameUi.VillageRecruitmentPanel is
                {
                    IsVisible: true,
                    OfferedWorkers: { } offeredWorkers,
                    CurrentWorkers: { } currentWorkers,
                })
            {
                var existingWorkersByAssignedSkill = villageScreen.Workers.GroupBy(x => x.Job.Type).ToDictionary(x => x.Key, x => x.ToList());
                foreach (var (workerElement, worker) in offeredWorkers.Where(x => x.IsVisible)
                             .Join(villageScreen.WorkersForSale, x => x.WorkerName, x => x.WorkerName, (el, d) => (el, d)))
                {
                    ShowUpgradeTooltips(existingWorkersByAssignedSkill, worker, workerElement, null, workersToReplace);
                }

                foreach (var (workerElement, worker) in currentWorkers.Where(x => x.IsVisible)
                             .Join(villageScreen.Workers, x => x.WorkerName, x => x.WorkerName, (el, d) => (el, d)))
                {
                    ShowUpgradeTooltips(existingWorkersByAssignedSkill, worker, workerElement, worker.Job.Type, workersToReplace);
                }
            }

            if (GameController.IngameState.IngameUi.VillageWorkerManagementPanel is
                {
                    IsVisible: true,
                    AvailableWorkers: { } availableWorkers,
                    AssignedWorkers: { } assignedWorkers,
                })
            {
                var existingWorkersByAssignedSkill = villageScreen.Workers.GroupBy(x => x.Job.Type).ToDictionary(x => x.Key, x => x.ToList());
                foreach (var (workerElement, worker) in availableWorkers.Where(x => x.IsVisible)
                             .Join(villageScreen.Workers, x => x.WorkerName, x => x.WorkerName, (el, d) => (el, d)))
                {
                    ShowUpgradeTooltips(existingWorkersByAssignedSkill, worker, workerElement, worker.Job.Type, workersToReplace);
                }

                foreach (var (workerElement, worker) in assignedWorkers.Where(x => x.IsVisible)
                             .Join(villageScreen.Workers, x => x.WorkerName, x => x.WorkerName, (el, d) => (el, d)))
                {
                    ShowUpgradeTooltips(existingWorkersByAssignedSkill, worker, workerElement, worker.Job.Type, []);
                }

                if (workersToReplace.Any())
                {
                    foreach (var workerToReplace in assignedWorkers.Where(x => x.IsVisible).IntersectBy(workersToReplace, x => x.WorkerName))
                    {
                        Graphics.DrawFrame(workerToReplace.GetClientRectCache, Settings.BadColor, 3);
                    }
                }
            }
        }


        // Render Shipment Helper
        if (GameController.IngameState.IngameUi.VillageShipmentScreen.IsVisible)
        {
            Vector2 offset = new Vector2(80, 80);
            var prepareShipment = GameController.IngameState.IngameUi.VillageShipmentScreen.GetChildFromIndices([3, 1, 0]);
            if (prepareShipment is not null && prepareShipment.IsVisible) offset += new Vector2(prepareShipment.GetClientRect().Size.Width, 0);
            // Iterate over selected Cities
            for (int i = 0; i <= 2; i++)
            {
                var node = GameController.IngameState.IngameUi.VillageShipmentScreen.GetChildFromIndices([3, 0, i]);
                var cityName = node?.GetChildFromIndices([7, 0, 0])?.Text;
                if (cityName == null) continue;
                
                var displayText = "";
                // Find Wanted Resources
                for (int j = 1; j <= 5; j++)
                {
                    var targetNode = GameController.IngameState.IngameUi.VillageShipmentScreen.GetChildFromIndices([2, 1, j]);
                    var cityTooltip = targetNode?.Tooltip;
                    if (cityTooltip == null || cityTooltip.Children[0].Text != cityName) continue;
                    var resourcesNode = cityTooltip.GetChildFromIndices([3, 0]);

                    for (int k = 0; k < resourcesNode.ChildCount; k++)
                    {
                        var resourceNode = resourcesNode.GetChildFromIndices([k]);
                        var resourceName = resourceNode[1].Text.Trim();
                        var resourcesNeeded = resourceNode[2].Text.Trim();
                        var alreadySend = Int32.Parse(new string(resourcesNeeded.TakeWhile(e => e != '/').ToArray()), NumberStyles.AllowThousands);
                        var wanted = Int32.Parse(new string(resourcesNeeded.SkipWhile(e => e != '/').Skip(1).TakeWhile(e => e != ' ').ToArray()), NumberStyles.AllowThousands);
                        var namePadded = resourceName.PadRight(15, ' ');
                        var resourcesPadded = $"{alreadySend} / {wanted}".PadRight(15, ' ');
                        var bonus = new string(resourcesNeeded.SkipWhile(e => e != '+').Skip(1).TakeWhile(e => e != '%').ToArray());
                        var bonusMulti = 1f;
                        if (float.TryParse(bonus, out bonusMulti))
                        {
                            bonusMulti = 1 + (bonusMulti / 100f);
                        };
                        var value = 1f;
                        ResourceValues.TryGetValue(resourceName, out value);
                        var resourcesNeededToFullfill = Math.Ceiling((wanted - alreadySend) / value * bonusMulti) ;
                        displayText += $"{namePadded} {resourcesPadded} Missing: {resourcesNeededToFullfill}\n";
                    }
                }
                
                Graphics.DrawTextWithBackground(displayText.Trim(), node.GetClientRect().TopRight.ToVector2Num() + offset, Color.Black);
            }
        }
    }

    private void ShowUpgradeTooltips(Dictionary<VillageJobType, List<VillageWorker>> existingWorkersByAssignedSkill, BaseVillageWorker worker, Element workerElement,
        VillageJobType assignedJob, HashSet<string> workersToReplace)
    {
        List<Action> tooltipActions = [];
        var indicatorType = 0;
        var moreLeftText = string.Empty;
        var rankedRanks = worker.JobRanks.ToList();
        var expectedWage = rankedRanks.Where(x => AverageWorkerWages.ContainsKey(x.Key.Id))
            .ToDictionary(r => r.Key, r => AverageWorkerWages[r.Key.Id] * GameController.Files.VillageJobSkillLevels.GetByLevel(r.Value).Speed);
        if (expectedWage.Any())
        {
            tooltipActions.Add(() =>
            {
                ImGui.Text("Expected wages:");
                if (ImGui.BeginTable("expected", 2, ImGuiTableFlags.Borders))
                {
                    var longestSkill = expectedWage.Max(x => x.Key.Id?.Length ?? 0);
                    ImGui.TableSetupColumn("Skill");
                    ImGui.TableSetupColumn("GPH");
                    ImGui.TableHeadersRow();
                    foreach (var skill in expectedWage.OrderByDescending(x => x.Value))
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text(skill.Key.Id?.PadLeft(longestSkill) ?? "");
                        ImGui.TableNextColumn();
                        ImGui.Text(skill.Value.ToString("F0").PadLeft(4));
                    }

                    ImGui.EndTable();
                }
            });
        }

        if (rankedRanks.All(x => AverageWorkerWages.ContainsKey(x.Key.Id)))
        {
            var maxWageSkill = expectedWage.MaxBy(x => x.Value);
            if (!Equals(assignedJob, null) && !Equals(assignedJob.Id, "Idling") && !Equals(maxWageSkill.Key, assignedJob) &&
                worker.WagePerHour > expectedWage.GetValueOrDefault(assignedJob, 0))
            {
                tooltipActions.Add(() =>
                {
                    var overpayFactor = expectedWage[maxWageSkill.Key] / expectedWage.GetValueOrDefault(assignedJob, AverageWorkerWages[assignedJob.Id]);
                    ImGui.TextColored(Settings.BadColor.Value.ToImguiVec4(),
                        $"Bad assignment: assigned to {assignedJob.Id}, but paid ~{(overpayFactor - 1) * 100:F0}%% more due to their {maxWageSkill.Key.Id} level");
                });
                moreLeftText += "\nBAD ASSIGN";
            }


            moreLeftText += $"\nWage: {(worker.WagePerHour / maxWageSkill.Value - 1) * 100:+#;-#;0}%";
        }

        foreach (var (candidateRankType, candidateRank) in worker.JobRanks.Where(x => !Equals(x.Key, assignedJob)))
        {
            var costPerPoint = worker.WagePerHour / (float)GameController.Files.VillageJobSkillLevels.GetByLevel(candidateRank).Speed;
            if (existingWorkersByAssignedSkill.GetValueOrDefault(candidateRankType, [])
                    .Where(x => x.JobRanks.GetValueOrDefault(candidateRankType, 0) <= candidateRank)
                    .Select(w => (worker: w,
                        costPerPoint: w.WagePerHour / (float)GameController.Files.VillageJobSkillLevels.GetByLevel(w.JobRanks.GetValueOrDefault(candidateRankType, 0)).Speed))
                    .OrderByDescending(x => x.costPerPoint)
                    .ToList() is { Count: > 0 } upgradeList)
            {
                var gphPerWorkUnitString = "GPH/work unit";
                if (upgradeList.Where(x => x.costPerPoint > costPerPoint).ToList() is { Count: > 0 } uList2)
                {
                    workersToReplace.Add(uList2[0].worker.WorkerName);
                    indicatorType = Math.Max(indicatorType, 2);
                    var longestWorkerName = uList2.Max(x => x.worker.WorkerName.Length);
                    tooltipActions.Add(() =>
                    {
                        ImGui.Text($"{worker.WorkerName} better at {candidateRankType.Id} ({costPerPoint:F2} gph/work unit, rank {candidateRank}, {worker.WagePerHour} gph) than:");
                        if (ImGui.BeginTable($"better##{candidateRankType.Id}", 5, ImGuiTableFlags.Borders))
                        {
                            ImGui.TableSetupColumn("Name");
                            ImGui.TableSetupColumn(gphPerWorkUnitString);
                            ImGui.TableSetupColumn("Rank");
                            ImGui.TableSetupColumn("GPH");
                            ImGui.TableSetupColumn("Current job");
                            ImGui.TableHeadersRow();
                            foreach (var (villageWorker, costPerSpeedI) in uList2)
                            {
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.Text(villageWorker.WorkerName.PadLeft(longestWorkerName));
                                ImGui.TableNextColumn();
                                ImGui.Text(costPerSpeedI.ToString("F2").PadLeft(gphPerWorkUnitString.Length));
                                ImGui.TableNextColumn();
                                ImGui.Text(villageWorker.JobRanks.GetValueOrDefault(candidateRankType, 0).ToString().PadLeft(4));
                                ImGui.TableNextColumn();
                                ImGui.Text(villageWorker.WagePerHour.ToString().PadLeft(4));
                                ImGui.TableNextColumn();
                                ImGui.Text(villageWorker.Job.Name switch { "" => $"Index {villageWorker.Job.Discriminator}", var s => s });
                            }

                            ImGui.EndTable();
                        }
                    });
                }
                else if (upgradeList.Where(x =>
                         {
                             var oldRank = x.worker.JobRanks.GetValueOrDefault(candidateRankType, 0);
                             return oldRank < candidateRank;
                         }).ToList() is { Count: > 0 } uList3)
                {
                    workersToReplace.Add(uList3[0].worker.WorkerName);
                    indicatorType = Math.Max(indicatorType, 1);
                    var longestWorkerName = uList3.Max(x => x.worker.WorkerName.Length);
                    tooltipActions.Add(() =>
                    {
                        ImGui.Text(
                            $"{worker.WorkerName} has higher {candidateRankType.Id} rank ({costPerPoint:F2} gph/work unit, rank {candidateRank}, {worker.WagePerHour} gph) than:");
                        if (ImGui.BeginTable($"higher##{candidateRankType.Id}", 5, ImGuiTableFlags.Borders))
                        {
                            ImGui.TableSetupColumn("Name");
                            ImGui.TableSetupColumn(gphPerWorkUnitString);
                            ImGui.TableSetupColumn("Rank");
                            ImGui.TableSetupColumn("GPH");
                            ImGui.TableSetupColumn("Current job");
                            ImGui.TableHeadersRow();
                            foreach (var (villageWorker, costPerSpeedI) in uList3)
                            {
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.Text(villageWorker.WorkerName.PadLeft(longestWorkerName));
                                ImGui.TableNextColumn();
                                ImGui.Text(costPerSpeedI.ToString("F2").PadLeft(gphPerWorkUnitString.Length));
                                ImGui.TableNextColumn();
                                ImGui.Text(villageWorker.JobRanks.GetValueOrDefault(candidateRankType, 0).ToString().PadLeft(4));
                                ImGui.TableNextColumn();
                                ImGui.Text(villageWorker.WagePerHour.ToString().PadLeft(4));
                                ImGui.TableNextColumn();
                                ImGui.Text(villageWorker.Job.Name switch { "" => $"Index {villageWorker.Job.Discriminator}", var s => s });
                            }

                            ImGui.EndTable();
                        }
                    });
                }
            }
        }

        switch (indicatorType)
        {
            case 2:
                Graphics.DrawTextWithBackground($"++{moreLeftText}", workerElement.GetClientRectCache.TopLeft.ToVector2Num(), Settings.GoodColor, FontAlign.Right, Color.Black);
                break;
            case 1:
                Graphics.DrawTextWithBackground($"+{moreLeftText}", workerElement.GetClientRectCache.TopLeft.ToVector2Num(), Settings.GoodColor, FontAlign.Right, Color.Black);
                break;
            case 0:
                Graphics.DrawTextWithBackground($"-{moreLeftText}", workerElement.GetClientRectCache.TopLeft.ToVector2Num(), FontAlign.Right, Color.Black);
                break;
        }

        if (tooltipActions.Any() && workerElement.GetClientRect().Contains(Input.MousePositionNum))
        {
            if (ImGui.BeginTooltip())
            {
                foreach (var tooltipAction in tooltipActions)
                {
                    tooltipAction();
                }

                ImGui.EndTooltip();
            }
        }
    }
}
