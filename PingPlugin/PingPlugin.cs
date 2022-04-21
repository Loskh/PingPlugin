﻿using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using PingPlugin.Attributes;
using PingPlugin.GameAddressDetectors;
using PingPlugin.PingTrackers;
using System;
using System.Dynamic;

namespace PingPlugin
{
    public class PingPlugin : IDalamudPlugin
    {
        [PluginService]
        [RequiredVersion("1.0")]
        public static DalamudPluginInterface PluginInterface { get; set; }

        [PluginService]
        [RequiredVersion("1.0")]
        public static CommandManager Commands { get; set; }

        [PluginService]
        [RequiredVersion("1.0")]
        public static ClientState ClientState { get; set; }

        [PluginService]
        [RequiredVersion("1.0")]
        public static DtrBar DtrBar { get; set; }

        private readonly PluginCommandManager<PingPlugin> pluginCommandManager;
        private readonly PingConfiguration config;

        private readonly PingTracker pingTracker;
        private readonly PingUI ui;

        internal ICallGateProvider<object, object> IpcProvider;

        public string Name => "PingPlugin";

        public PingPlugin()
        {
            this.config = (PingConfiguration)PluginInterface.GetPluginConfig() ?? new PingConfiguration();
            this.config.Initialize(PluginInterface);

            this.pingTracker = new AggregatePingTracker(this.config, new AggregateAddressDetector(ClientState));

            InitIpc();

            this.ui = new PingUI(this.pingTracker, PluginInterface, DtrBar, this.config);
            this.pingTracker.OnPingUpdated += this.ui.UpdateDtrBarPing;

            PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
            PluginInterface.UiBuilder.Draw += this.ui.BuildUi;

            this.pluginCommandManager = new PluginCommandManager<PingPlugin>(this, Commands);
        }

        private void InitIpc()
        {
            try
            {
                IpcProvider = PluginInterface.GetIpcProvider<object, object>("PingPlugin.Ipc");
                this.pingTracker.OnPingUpdated += payload =>
                {
                    dynamic obj = new ExpandoObject();
                    obj.LastRTT = payload.LastRTT;
                    obj.AverageRTT = payload.AverageRTT;
                    IpcProvider.SendMessage(obj);
                };
            }
            catch (Exception e)
            {
                PluginLog.Error($"Error registering IPC provider:\n{e}");
            }
        }

        [Command("/ping")]
        [HelpMessage("Show/hide the ping monitor.")]
        [ShowInHelp]
        public void PingCommand(string command, string args)
        {
            this.config.MonitorIsVisible = !this.config.MonitorIsVisible;
            this.config.Save();
        }

        [Command("/pinggraph")]
        [HelpMessage("Show/hide the ping graph.")]
        [ShowInHelp]
        public void PingGraphCommand(string command, string args)
        {
            this.config.GraphIsVisible = !this.config.GraphIsVisible;
            this.config.Save();
        }

        [Command("/pingconfig")]
        [HelpMessage("Show PingPlugin's configuration.")]
        [ShowInHelp]
        public void PingConfigCommand(string command, string args)
        {
            this.ui.ConfigVisible = true;
        }

        private void OpenConfigUi()
        {
            this.ui.ConfigVisible = true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.pluginCommandManager.Dispose();

            PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
            PluginInterface.UiBuilder.Draw -= this.ui.BuildUi;

            this.config.Save();

            this.pingTracker.OnPingUpdated -= this.ui.UpdateDtrBarPing;
            this.ui.Dispose();

            this.pingTracker.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
