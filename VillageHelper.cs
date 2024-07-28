using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using SharpDX;

namespace VillageHelper;

public class VillageHelper : BaseSettingsPlugin<VillageHelperSettings>
{
    public override bool Initialise()
    {
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

        if (Settings.WindowShown &&
            GameController.IngameState.IngameUi.VillageScreen.ZoneLoadResources.Resources is { Count: > 0 } resources &&
            ImGui.Begin("Village stats"))
        {
            var villageGold = GameController.IngameState.IngameUi.VillageScreen.ZoneLoadResources.Gold;
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
                            var enoughResource = requiredResource.Item2 >= resources.GetValueOrDefault(requiredResource.Item1);
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

                    ImGui.Text($"{villageGold} gold");
                    if (colorSet)
                    {
                        ImGui.PopStyleColor();
                    }
                }
                ImGui.TreePop();
            }

            if (Settings.ShowShipments &&
                GameController.IngameState.IngameUi.VillageScreen.RemainingShipmentTimes is { Count: > 0 } &&
                ImGui.TreeNodeEx("Shipments", ImGuiTreeNodeFlags.DefaultOpen))
            {
                foreach (var remainingShipmentTime in GameController.IngameState.IngameUi.VillageScreen.RemainingShipmentTimes)
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
        }
    }
}