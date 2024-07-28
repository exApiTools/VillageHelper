using System.Windows.Forms;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace VillageHelper;

public class VillageHelperSettings : ISettings
{
    //Mandatory setting to allow enabling/disabling your plugin
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    public HotkeyNode ToggleWindowHotkey { get; set; } = new HotkeyNode(Keys.None);
    public ToggleNode ShowResources { get; set; } = new ToggleNode(true);
    public ToggleNode ShowShipments { get; set; } = new ToggleNode(true);
    public ToggleNode ShowUpgrades { get; set; } = new ToggleNode(true);
    public ToggleNode ShowEmptyResources { get; set; } = new ToggleNode(true);
    public ToggleNode ShowEmptyResourcesInColor { get; set; } = new ToggleNode(true);
    public ColorNode GoodColor { get; set; } = new ColorNode(Color.Green);
    public ColorNode BadColor { get; set; } = new ColorNode(Color.Red);
    public bool WindowShown = true;
}