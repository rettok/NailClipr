﻿using System;
using System.Linq;
using System.Diagnostics;
using System.Windows.Forms;
using EliteMMO.API;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Net;
using System.Text.RegularExpressions;

namespace NailClipr
{
    public partial class NailClipr : Form
    {
        private static EliteAPI api;
        private BackgroundWorker bw = new BackgroundWorker();
        private BackgroundWorker cw = new BackgroundWorker();

        public static Player Player = new Player();

        public static ComboBox GUI_WARP;
        public static CheckBox GUI_MAINT;
        public static CheckBox GUI_TOPMOST;
        public static CheckBox GUI_PLAYER_DETECT;
        public static Button GUI_ACCEPT;

        public static Label GUI_TARGET;
        public static Label GUI_NEAREST_PLAYER;
        public static Label GUI_STATUS;
        public static Label GUI_ZONE;
        public static Label GUI_DEFAULT_SPEED;
        public static Label GUI_SPEED;
        public static TrackBar GUI_SPEED_DEFAULT_TRACK;
        public static TrackBar GUI_SPEED_TRACK;

        public NailClipr()
        {
            if (!Debugger.IsAttached)
            {
                CheckUpdate();
            }
            InitializeComponent();
            AssignControls();
            PostInit();
            SelectProcess();

            // Start the background worker..
            bw.DoWork += new DoWorkEventHandler(bw_DoWork);
            bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
            bw.WorkerSupportsCancellation = true;
            bw.WorkerReportsProgress = true;
            bw.RunWorkerAsync();


            // Start the background worker..
            cw.DoWork += new DoWorkEventHandler(cw_DoWork);
            cw.WorkerSupportsCancellation = true;
            cw.RunWorkerAsync();
        }
        public void PostInit()
        {
            XML.LoadAreas();
            XML.LoadWarps();
            XML.LoadSettings();
        }

        public void AssignControls()
        {
            GUI_WARP = CB_Warp;
            GUI_MAINT = ChkBox_Maint;
            GUI_TOPMOST = ChkBox_StayTop;
            GUI_PLAYER_DETECT = ChkBox_PlayerDetect;
            GUI_ACCEPT = Btn_Accept;

            GUI_TARGET = Lbl_TargetInfo;
            GUI_NEAREST_PLAYER = Lbl_NearestPlayer;
            GUI_STATUS = Lbl_Status;
            GUI_ZONE = Lbl_Zone;
            GUI_SPEED = Lbl_SpeedVar;
            GUI_DEFAULT_SPEED = Lbl_DefaultSpeed;
            GUI_SPEED_DEFAULT_TRACK = Bar_Speed_Default;
            GUI_SPEED_TRACK = Bar_Speed;

            Lbl_Ver.Text = "v." + Structs.App.ver;
        }

        public void SelectProcess()
        {
            #region Final Fantasy XI [POL]
            var data = Process.GetProcessesByName("pol");

            if (data.Count() != 0)
            {
                var proc = Process.GetProcessesByName("pol").First().Id;
                api = new EliteAPI(proc);
                string p = api.Entity.GetLocalPlayer().Name;
                WebClient client = new WebClient();
                Stream stream = client.OpenRead("https://github.com/mattlemmone/NailClipr/raw/master/auth.txt");
                StreamReader reader = new StreamReader(stream);
                String content = reader.ReadToEnd();

                if (content.Length == 0 || p.Length == 0 || !content.Contains(p))
                {
                    MessageBox.Show(Structs.Error.Auth.text, Structs.Error.Auth.title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ExitApp();
                }
                Structs.Speed.whitelist = content.Split(',').ToList();
                this.Text = Structs.App.name + " - " + p;

            }
            else
            {
                MessageBox.Show(Structs.Error.Exit.text, Structs.Error.Exit.title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                ExitApp();
            }
            #endregion
        }

        private void CheckUpdate()
        {
            string apidll = "";
            string mmodll = "";
            string appexe = "";
            if (File.Exists(Application.StartupPath + @"\EliteAPI.dll"))
                apidll = FileVersionInfo.GetVersionInfo(Application.StartupPath + @"\EliteAPI.dll").FileVersion;
            if (File.Exists(Application.StartupPath + @"\EliteMMO.API.dll"))
                mmodll = FileVersionInfo.GetVersionInfo(Application.StartupPath + @"\EliteMMO.API.dll").FileVersion;
            appexe = Structs.App.ver;

            string api, mmo, exe;
            api = GetStringFromUrl("http://ext.elitemmonetwork.com/downloads/eliteapi/index.php?v");
            mmo = GetStringFromUrl("http://ext.elitemmonetwork.com/downloads/elitemmo_api/index.php?v");
            exe = Regex.Replace(GetStringFromUrl("https://raw.githubusercontent.com/mattlemmone/NailClipr/master/ver.txt"), @"\t|\n|\r", "");

            if (apidll == "" || api != apidll || appexe == "" || exe != appexe || mmodll == "" || mmo != mmodll)
            {
                Process.Start(Application.StartupPath + @"\Updater.exe");
                ExitApp();
            }
        }

        private string GetStringFromUrl(string location)
        {
            WebRequest request = WebRequest.Create(location);
            WebResponse response = request.GetResponse();
            Stream dataStream = response.GetResponseStream();
            StreamReader reader = new StreamReader(dataStream);
            string responseFromServer = reader.ReadToEnd();
            return responseFromServer;
        }
        private void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            while (worker.CancellationPending != true)
            {
                System.Threading.Thread.Sleep(100);
                bw.ReportProgress(0);

                workerOverwrites();
            }
        }

        private void cw_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            while (!worker.CancellationPending)
            {
                Thread.Sleep(TimeSpan.FromSeconds(0.1));
                Functions.ParseChat(api);
            }
        }

        public void workerOverwrites()
        {
            //Constantly write maintenance mode in case it gets overwritten.
            Structs.Status.PreventOverwrite(api);

            /*Speed*/
            //Not initialized.
            if (Player.Speed.expected == 0 && api.Player.Speed <= Structs.Speed.MAX)
            {
                Player.Speed.expected = api.Player.Speed;
            }

            //Turn speed off around other players.
            if (Structs.settings.playerDetection) Functions.PlayersRendered(api);

            Structs.Speed.PreventOverWrite(api);

        }

        private void bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            //Update GUI.
            Player.Location.isZoning = api.Player.X == 0 && api.Player.Y == 0 && api.Player.Z == 0;
            if (Player.Location.isZoning && Player.hasDialogue)
                Player.hasDialogue = false;

            Functions.UpdateLabels(api);

            GUI_ACCEPT.Enabled = Player.hasDialogue;
        }

        private void ChkBox_Maint_CheckedChanged(object sender, EventArgs e)
        {
            Player.MaintenanceMode(api, ChkBox_Maint.Checked);
        }

        private void ChkBox_DetectDisable_CheckedChanged(object sender, EventArgs e)
        {

            Structs.settings.playerDetection = ChkBox_PlayerDetect.Checked;
        }

        private void ChkBox_StayTop_CheckedChanged(object sender, EventArgs e)
        {
            Structs.settings.topMostForm = ChkBox_StayTop.Checked;
            this.TopMost = ChkBox_StayTop.Checked;
        }

        private void Bar_Speed_Default_Scroll(object sender, EventArgs e)
        {
            float barVal = GUI_SPEED_DEFAULT_TRACK.Value / Structs.Speed.DIVISOR;
            float speed = barVal + Structs.Speed.NATURAL;
            Player.Speed.normal = speed;
            GUI_DEFAULT_SPEED.Text = "x" + speed / Structs.Speed.NATURAL;
        }

        private void Bar_Speed_Scroll(object sender, EventArgs e)
        {
            float barVal = GUI_SPEED_TRACK.Value / Structs.Speed.DIVISOR;
            float speed = barVal + Structs.Speed.NATURAL;

            //Fallback. Never can be too safe with speed mods.
            if (speed <= Structs.Speed.MAX)
            {
                Player.Speed.expected = speed;
                api.Player.Speed = speed;
                GUI_SPEED.Text = "x" + speed / Structs.Speed.NATURAL;
            }
        }


        private void Btn_Plus_X_Click(object sender, EventArgs e)
        {
            api.Player.X = api.Player.X + Structs.Settings.POS_INC;
        }

        private void Btn_Minus_X_Click(object sender, EventArgs e)
        {

            api.Player.X = api.Player.X - Structs.Settings.POS_INC;
        }

        private void Btn_Plus_Y_Click(object sender, EventArgs e)
        {

            api.Player.Y = api.Player.Z + Structs.Settings.POS_INC;
        }

        private void Btn_Minus_Y_Click(object sender, EventArgs e)
        {

            api.Player.Y = api.Player.Z - Structs.Settings.POS_INC;
        }

        private void Btn_Plus_Z_Click(object sender, EventArgs e)
        {

            api.Player.Z = api.Player.Y + Structs.Settings.POS_INC;
        }

        private void Btn_Minus_Z_Click(object sender, EventArgs e)
        {

            api.Player.Z = api.Player.Y - Structs.Settings.POS_INC;
        }

        private void Btn_Save_Click(object sender, EventArgs e)
        {
            XML.SaveWarp(api);
        }

        private void Btn_Warp_Click(object sender, EventArgs e)
        {
            Player.Warp(api);
        }

        private void Form_Close(object sender, EventArgs e)
        {
            api.Player.Speed = Player.Speed.normal;
        }

        private void Btn_Delete_Click(object sender, EventArgs e)
        {
            XML.DeleteWarp(api);
        }

        private void Btn_SaveSettings_Click(object sender, EventArgs e)
        {
            XML.SaveSettings();
        }

        private void Btn_Accept_Click(object sender, EventArgs e)
        {
            api.ThirdParty.SendString("/echo " + Structs.Chat.Warp.acceptSelfNotify);
            api.ThirdParty.SendString("/p " + Structs.Chat.Warp.acceptNotify);
            Player.Warp(api, true);
        }

        private void Btn_Req_Click(object sender, EventArgs e)
        {
            string s = Math.Round(api.Player.X, 5) + " " + Math.Round(api.Player.Z, 5) + " " + Math.Round(api.Player.Y, 5) + " " + api.Player.ZoneId;
            api.ThirdParty.SendString("/p " + s);
        }

        public static void ExitApp()
        {
            if (System.Windows.Forms.Application.MessageLoop)
            {
                // WinForms app
                System.Windows.Forms.Application.Exit();
            }
            else
            {
                // Console app
                System.Environment.Exit(1);
            }
        }

        private void Btn_NW_Click(object sender, EventArgs e)
        {
            float PtX = api.Player.X;
            float PtX1 = PtX - Structs.Settings.POS_INC;
            float PtY = api.Player.Z;
            float PtY1 = PtY + Structs.Settings.POS_INC;
            float[] pts = MidPoint(PtX, PtX1, PtY, PtY1);
            api.Player.X = pts[0];
            api.Player.Y = pts[1];
        }

        private float[] MidPoint(float A, float A1, float B, float B1)
        {
            float[] ret = { (A + A1) / 2, (B + B1) / 2 };
            return ret;
        }

        private void Btn_SW_Click(object sender, EventArgs e)
        {
            float PtX = api.Player.X;
            float PtX1 = PtX - Structs.Settings.POS_INC;
            float PtY = api.Player.Z;
            float PtY1 = PtY - Structs.Settings.POS_INC;
            float[] pts = MidPoint(PtX, PtX1, PtY, PtY1);
            api.Player.X = pts[0];
            api.Player.Y = pts[1];
        }

        private void Btn_NE_Click(object sender, EventArgs e)
        {
            float PtX = api.Player.X;
            float PtX1 = PtX + Structs.Settings.POS_INC;
            float PtY = api.Player.Z;
            float PtY1 = PtY + Structs.Settings.POS_INC;
            float[] pts = MidPoint(PtX, PtX1, PtY, PtY1);
            api.Player.X = pts[0];
            api.Player.Y = pts[1];
        }

        private void Btn_SE_Click(object sender, EventArgs e)
        {
            float PtX = api.Player.X;
            float PtX1 = PtX + Structs.Settings.POS_INC;
            float PtY = api.Player.Z;
            float PtY1 = PtY - Structs.Settings.POS_INC;
            float[] pts = MidPoint(PtX, PtX1, PtY, PtY1);
            api.Player.X = pts[0];
            api.Player.Y = pts[1];
        }
    }
}



