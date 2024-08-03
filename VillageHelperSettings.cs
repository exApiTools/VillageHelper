using System.Windows.Forms;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using Newtonsoft.Json;
using SharpDX;

namespace VillageHelper;

public class VillageHelperSettings : ISettings
{
    //Mandatory setting to allow enabling/disabling your plugin
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    public HotkeyNode ToggleWindowHotkey { get; set; } = new HotkeyNode(Keys.None);
    public ToggleNode ShowResources { get; set; } = new ToggleNode(true);
    public ToggleNode ShowActions { get; set; } = new ToggleNode(true);
    public ToggleNode ShowUpgrades { get; set; } = new ToggleNode(true);
    public ToggleNode ShowEmptyResources { get; set; } = new ToggleNode(true);
    public ToggleNode ShowEmptyResourcesInColor { get; set; } = new ToggleNode(true);
    public ToggleNode ShowStatusOverlay { get; set; } = new ToggleNode(true);
    public ToggleNode ShowProjectedCurrentGold { get; set; } = new ToggleNode(false);

    public ToggleNode ShowWorkerUpgradeTips { get; set; } = new ToggleNode(true);
    public ColorNode GoodColor { get; set; } = new ColorNode(Color.Green);
    public ColorNode NeutralColor { get; set; } = new ColorNode(Color.White);
    public ColorNode BadColor { get; set; } = new ColorNode(Color.Red);

    [JsonIgnore]
    public ButtonNode ExportWorkerStats { get; set; } = new ButtonNode();
    public bool WindowShown = true;
}