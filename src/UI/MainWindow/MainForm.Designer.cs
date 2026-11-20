using AudioForge.UI.MainWindow.Options.ThemeOptions;
using AudioForge.UI.MainWindow.Ribbons;
using AudioForge.UI.TransportBar;

namespace AudioForge.UI.MainWindow;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.Panel mainContentPanel;
    private TabControl _mainTabControl;
    private TransportBarControl _dockedTransportBar;

    private TabPage tabPageMedia;
    private TabPage tabPageInternetRadio;
    private TabPage tabPageGraphicEqualizer;
    private TabPage tabPageVisualizer;
    private TabPage tabPageMixer;
    private TabPage tabPageNetworkedSpeakers;
    private TabPage tabPagePlugins;
    private TabPage tabPageOptions;

    private MediaRibbonControl mediaRibbonControl;
    private InternetRadioRibbonControl internetRadioRibbonControl;
    private GraphicEqualiserRibbonControl graphicEqualiserRibbonControl;
    private VisualiserRibbonControl visualiserRibbonControl;
    private MixerRibbonControl mixerRibbonControl;
    private NetworkedSpeakersRibbonControl networkedSpeakersRibbonControl;
    private PluginsRibbonControl pluginsRibbonControl;
    private OptionsRibbonControl optionsRibbonControl;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.mainContentPanel = new System.Windows.Forms.Panel();
        this._mainTabControl = new System.Windows.Forms.TabControl();
        this._dockedTransportBar = new TransportBarControl();

        this.tabPageMedia = new System.Windows.Forms.TabPage();
        this.tabPageInternetRadio = new System.Windows.Forms.TabPage();
        this.tabPageGraphicEqualizer = new System.Windows.Forms.TabPage();
        this.tabPageVisualizer = new System.Windows.Forms.TabPage();
        this.tabPageMixer = new System.Windows.Forms.TabPage();
        this.tabPageNetworkedSpeakers = new System.Windows.Forms.TabPage();
        this.tabPagePlugins = new System.Windows.Forms.TabPage();
        this.tabPageOptions = new System.Windows.Forms.TabPage();

        this.mediaRibbonControl = new MediaRibbonControl();
        this.internetRadioRibbonControl = new InternetRadioRibbonControl();
        this.graphicEqualiserRibbonControl = new GraphicEqualiserRibbonControl();
        this.visualiserRibbonControl = new VisualiserRibbonControl();
        this.mixerRibbonControl = new MixerRibbonControl();
        this.networkedSpeakersRibbonControl = new NetworkedSpeakersRibbonControl();
        this.pluginsRibbonControl = new PluginsRibbonControl();
        this.optionsRibbonControl = new OptionsRibbonControl();

        this.mainContentPanel.SuspendLayout();
        this._mainTabControl.SuspendLayout();
        this.SuspendLayout();

        //
        // mainContentPanel
        //
        this.mainContentPanel.Controls.Add(this._mainTabControl);
        this.mainContentPanel.Dock = System.Windows.Forms.DockStyle.Fill;

        //
        // _mainTabControl
        //
        this._mainTabControl.Controls.Add(this.tabPageMedia);
        this._mainTabControl.Controls.Add(this.tabPageInternetRadio);
        this._mainTabControl.Controls.Add(this.tabPageGraphicEqualizer);
        this._mainTabControl.Controls.Add(this.tabPageVisualizer);
        this._mainTabControl.Controls.Add(this.tabPageMixer);
        this._mainTabControl.Controls.Add(this.tabPageNetworkedSpeakers);
        this._mainTabControl.Controls.Add(this.tabPagePlugins);
        this._mainTabControl.Controls.Add(this.tabPageOptions);
        this._mainTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
        this._mainTabControl.SizeMode = System.Windows.Forms.TabSizeMode.Normal;

        //
        // Tab pages - each hosts its ribbon docked to the top
        //
        this.tabPageMedia.Text = "Media";
        this.tabPageMedia.Controls.Add(this.mediaRibbonControl);
        this.mediaRibbonControl.Dock = System.Windows.Forms.DockStyle.Top;

        this.tabPageInternetRadio.Text = "Internet radio";
        this.tabPageInternetRadio.Controls.Add(this.internetRadioRibbonControl);
        this.internetRadioRibbonControl.Dock = System.Windows.Forms.DockStyle.Top;

        this.tabPageGraphicEqualizer.Text = "Graphic Equalizer";
        this.tabPageGraphicEqualizer.Controls.Add(this.graphicEqualiserRibbonControl);
        this.graphicEqualiserRibbonControl.Dock = System.Windows.Forms.DockStyle.Top;

        this.tabPageVisualizer.Text = "Visualizer";
        this.tabPageVisualizer.Controls.Add(this.visualiserRibbonControl);
        this.visualiserRibbonControl.Dock = System.Windows.Forms.DockStyle.Top;

        this.tabPageMixer.Text = "Mixer";
        this.tabPageMixer.Controls.Add(this.mixerRibbonControl);
        this.mixerRibbonControl.Dock = System.Windows.Forms.DockStyle.Top;

        this.tabPageNetworkedSpeakers.Text = "Networked Speakers";
        this.tabPageNetworkedSpeakers.Controls.Add(this.networkedSpeakersRibbonControl);
        this.networkedSpeakersRibbonControl.Dock = System.Windows.Forms.DockStyle.Top;

        this.tabPagePlugins.Text = "Plugins";
        this.tabPagePlugins.Controls.Add(this.pluginsRibbonControl);
        this.pluginsRibbonControl.Dock = System.Windows.Forms.DockStyle.Top;

        this.tabPageOptions.Text = "Options";
        this.tabPageOptions.Controls.Add(this.optionsRibbonControl);
        this.optionsRibbonControl.Dock = System.Windows.Forms.DockStyle.Top;

        //
        // _dockedTransportBar
        //
        this._dockedTransportBar.Dock = System.Windows.Forms.DockStyle.Bottom;
        this._dockedTransportBar.Height = 100;

        //
        // MainForm
        //
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.Controls.Add(this.mainContentPanel);
        this.Controls.Add(this._dockedTransportBar);
        this.ClientSize = new System.Drawing.Size(1024, 768);
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "Audio Forge with Helix Sound Engine";

        this.mainContentPanel.ResumeLayout(false);
        this._mainTabControl.ResumeLayout(false);
        this.ResumeLayout(false);
    }
}
