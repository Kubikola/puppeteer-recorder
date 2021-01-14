﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Frontend.Forms;
using Frontend.UserControls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Frontend
{
    /// <summary>
    /// This is the main form of the Frontend application.
    /// After the start, the list of thumbnails (recording previews) is shown.
    /// </summary>
    public partial class MainForm : Form
    {
        List<Form> openedSettingForms = new List<Form>();

        public enum AppMode
        {
            List, //list of thumbnails is shown
            Edit //some recording is edited
        }

        //current state of the application
        private AppMode appState = AppMode.List;

        //currently edited recording
        private CurrentEdit currentEdit;

        public  AppMode AppState
        {
            get { return appState; }
            set
            {
                appState = value;
                UiChange();
            }
        }

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            editUserControl.InitMain(this);
            List<Thumbnail> thumbnails = ThumbnailManager.Init();
            LoadThumbnailsUi(thumbnails);

            PerformSettingsChecks();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (editUserControl.NodeJsProcess != null && !editUserControl.NodeJsProcess.HasExited)
                editUserControl.NodeJsProcess.Kill();
        }

        /// <summary>
        /// Showing form of recorder settings on menu item click.
        /// </summary>
        private void recorderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RecorderSettingsForm rs = new RecorderSettingsForm();
            rs.Closing += (o, args) =>
            {
                RecorderConfiguration rc = rs.ExportRecorderOptions();
                ConfigManager.SavePuppeteerConfiguration(rc.PuppeteerOptions);
                ConfigManager.SaveRecordedEventsConfiguration(rc.RecordedEvents);
              
            };
            openedSettingForms.Add(rs);
            rs.Show();
        }

        /// <summary>
        /// Showing form of code generator settings on menu item click.
        /// </summary>
        private void codeGeneratorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CodeGenSettingsForm cgs = new CodeGenSettingsForm();
            cgs.BindCodeGeneratorOptions(ConfigManager.GetCodeGeneratorOptions());

            cgs.Closing += (o, args) =>
            {
                CodeGenOptions cgo = cgs.ExportCodeGeneratorOptions();
                ConfigManager.SaveCodeGeneratorOptions(cgo);
            };
            openedSettingForms.Add(cgs);
            cgs.Show();
        }

        /// <summary>
        /// Showing form of player settings on menu item click.
        /// </summary>
        private void playerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PlayerForm pf = new PlayerForm();
            pf.BindOptions(ConfigManager.GetPlayerOptions());
            pf.Closing += (o, args) =>
            {
                PlayerOptions po = pf.ExportOptions();
                ConfigManager.SavePlayerOptions(po);
            };
            openedSettingForms.Add(pf);
            pf.Show();
        }

        /// <summary>
        /// On menu item click, this method shows form of settings related to Node.js
        /// </summary>
        private void nodejsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NodeJsConfig njc = new NodeJsConfig(this);
            njc.Closing += (o, args) =>
            {
                ConfigManager.SaveNodeJsOptions(njc.ExportNodeJsOptions());
            };
            njc.BindNodeJsOptions(ConfigManager.GetNodeJsOptions());
            openedSettingForms.Add(njc);
            njc.Show();
        }

        /// <summary>
        /// Creates corresponding UIs to passed thumbnails list
        /// </summary>
        private void LoadThumbnailsUi(List<Thumbnail> thumbnails)
        {
            foreach(Thumbnail t in thumbnails)
            {
                ThumbnailUserControl tuc = new ThumbnailUserControl();
                tuc.BindThumbnail(t);
                thumbnailsFlowLayoutPanel.Controls.Add(tuc);
            }
            
        }

        /// <summary>
        /// Changes UI and prepares for editing passed thumbnail
        /// </summary>
        public void SwitchToEditMode(Thumbnail t)
        {
            openedSettingForms.ForEach(f=>f.Close());
            openedSettingForms.Clear();

            if (currentEdit == null)
                currentEdit = new CurrentEdit();

            currentEdit.Config = new Configuration
            {
                PuppeteerConfig = ConfigManager.GetPuppeteerConfiguration(),
                CodeGenConfig = ConfigManager.GetCodeGeneratorOptions(),
                PlayerOptions = ConfigManager.GetPlayerOptions(),
                NodeJsOptions = ConfigManager.GetNodeJsOptions(),
                RecordedEvents = ConfigManager.GetRecordedEventsOptions()
            };
            currentEdit.Thumbnail = t;
            if (IsNewRecording(t))
            {
                currentEdit.Recordings = new List<Recording>();
                currentEdit.NextAvailableId = 0;
            }

            else //editing an existing recording
            {
                string recordingJson = File.ReadAllText($"recordings/{t.Id}.json");
                dynamic recordings = JsonConvert.DeserializeObject(recordingJson, ConfigManager.JsonSettings);
                currentEdit.StartupHints = recordings.StartupHints;
                currentEdit.Recordings = (List<Recording>) ((JArray) recordings.Recordings).ToObject(typeof(List<Recording>));
                currentEdit.NextAvailableId = recordings.NextId;
            }

            editUserControl.BindEdit(currentEdit);

            AppState = AppMode.Edit;
        }


        private bool IsNewRecording(Thumbnail thumbnail)
        {
            return !File.Exists($"recordings/{thumbnail.Id}.json");
        }

        /// <summary>
        /// Changes UI from List mode to Edit mode and vice versa.
        /// </summary>
        private void UiChange()
        {
            if (AppState == AppMode.List)
            {
                SetListUiVisibility(true);
                SetEditUiVisiblity(false);

                thumbnailsFlowLayoutPanel.Controls.Clear();
                LoadThumbnailsUi(ThumbnailManager.LoadThumbnails());
            }
            else //Edit
            {
                SetListUiVisibility(false);
                SetEditUiVisiblity(true);
            }
        }

        /// <summary>
        /// Sets the visibility of menu strip items.
        /// </summary>
        private void SetEditUiVisiblity(bool state)
        {
            editUserControl.Visible = state;
            menuStrip.Visible = !state;
        }

        /// <summary>
        /// Sets the visibility of List mode components.
        /// </summary>
        /// <param name="state"></param>
        private void SetListUiVisibility(bool state)
        {
            addNewRecordingButton.Visible = state;
            thumbnailsFlowLayoutPanel.Visible = state;
            nameLabel.Visible = state;
            websitesLabel.Visible = state;
            createdLabel.Visible = state;
            updatedLabel.Visible = state;
            menuStrip.Visible = !state;
            nodeInterpreterVersionLabel.Visible = state;
        }

        /// <summary>
        /// After clicking on the"Add Recording" button, this method creates new thumbnails and switches to edit mode.
        /// </summary>
        private void addNewRecordingButton_Click(object sender, EventArgs e)
        {
            Thumbnail t = ThumbnailManager.NewThumbnail();
            SwitchToEditMode(t);
        }

        /// <summary>
        /// Sets the availability of edit mode.
        /// E.g. edit mode is supposed to be disabled when Node.js interpreter is not found.
        /// </summary>
        private void SetEditModeAvailability(bool state)
        {
            thumbnailsFlowLayoutPanel.Enabled = state;
            addNewRecordingButton.Enabled = state;
        }

        /// <summary>
        /// Performs a given action that might be running on a different thread and updates UI safely.
        /// </summary>
        private void UiSafeOperation(Action a)
        {
            if (InvokeRequired)
                Invoke(new MethodInvoker(() => a()));

            else
                a();
        }

        /// <summary>
        /// Performs certain checks to know whether edit mode should be available.
        /// E.g. checks whether the path points to the correct Node.js interpreter.
        /// </summary>
        public void PerformSettingsChecks()
        {
            NodeJsOptions njo = ConfigManager.GetNodeJsOptions();
            Process p = new Process();
            p.EnableRaisingEvents = true;
            ProcessStartInfo psi = new ProcessStartInfo(njo.InterpreterPath, "-v");
            psi.UseShellExecute = false;
            psi.RedirectStandardError = true;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            p.Exited += (sender, args) =>
            {
                string nodeV = p.StandardOutput.ReadToEnd();

                if (nodeV.StartsWith("v"))
                {
                    UiSafeOperation(() =>
                    {
                        nodeInterpreterVersionLabel.Text = $"node -v: {nodeV}";
                        SetEditModeAvailability(true);
                    });
                }
                else
                    UiSafeOperation(NodeInterpterNotWorking);
                    
                
            };
            p.StartInfo = psi;
            try
            {
                p.Start();
            }
            catch (Exception)
            {
                UiSafeOperation(NodeInterpterNotWorking);
            }
        }

        /// <summary>
        /// Updates UI with a message reporting a problem with Node.js interpreter.
        /// </summary>
        private void NodeInterpterNotWorking()
        {
            nodeInterpreterVersionLabel.Text = "node -v: Problems with starting node interpreter, check node.js interpreter path in options.";
            SetEditModeAvailability(false);
        }

        /// <summary>
        /// Global application shortcut Shift + Del that deletes focused action (only works in edit mode).
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (AppState == AppMode.Edit)
            {
                if (keyData == (Keys.Shift | Keys.Delete))
                {
                    editUserControl.DeleteRequested();
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
