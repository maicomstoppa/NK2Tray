﻿using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;


namespace NK2Tray
{
    public class SysTrayApp : Form
    {
        [STAThread]
        public static void Main() => Application.Run(new SysTrayApp());

        private NotifyIcon trayIcon;
        public MidiDevice midiDevice;
        public AudioDevice audioDevices;

        public SysTrayApp()
        {
            System.AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;
            Console.WriteLine($@"NK2 Tray {DateTime.Now}");
            trayIcon = new NotifyIcon
            {
                Text = "NK2 Tray",
                Icon = new Icon(Properties.Resources.nk2tray, 40, 40),

                ContextMenu = new ContextMenu()
            };
            trayIcon.ContextMenu.Popup += OnPopup;

            trayIcon.Visible = true;

            SetupDevice();
        }

        private Boolean SetupDevice()
        {
            audioDevices = new AudioDevice();

            midiDevice = new NanoKontrol2(audioDevices);
            if (!midiDevice.Found)
                midiDevice = new XtouchMini(audioDevices);

            audioDevices.midiDevice = midiDevice;

            return midiDevice.Found;
        }

        private void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine(e.ExceptionObject.ToString());
            MessageBox.Show(e.ExceptionObject.ToString(), "NK2 Tray Error", MessageBoxButtons.OK);

            if (midiDevice != null)
            {
                try
                {
                    midiDevice.ResetAllLights();
                    midiDevice.faders.Last().SetRecordLight(true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
            Application.Exit();
        }

        private String getProgramLabel(Fader fader)
        {
            if (fader.assigned)
                return fader.assignment.label;

            if(fader.identifier.Length > 0 && fader.identifier.Contains(".exe"))
            {
                String identifier = fader.identifier;
                int lastBackSlash = identifier.LastIndexOf('\\') + 1;
                int programNameLength = identifier.IndexOf(".exe") - lastBackSlash;
                String progamName = identifier.Substring(lastBackSlash, programNameLength);
                return progamName;
            }
            return "";
        }

        private void OnPopup(object sender, EventArgs e)
        {
            ContextMenu trayMenu = (ContextMenu)sender;
            trayMenu.MenuItems.Clear();

            var mixerSessions = audioDevices.GetCachedMixerSessions();

            var masterMixerSessionList = new List<MixerSession>();
            foreach(MMDevice mmDevice in audioDevices.devices)
            {
                masterMixerSessionList.Add(new MixerSession(mmDevice.ID, audioDevices, "Master", SessionType.Master));
            }
            
            MixerSession focusMixerSession;
            focusMixerSession = new MixerSession("", audioDevices, "Focus", SessionType.Focus);
                        
            // Dont create context menu if no midi device is connected
            if(!midiDevice.Found)
            {
                if (!SetupDevice()) // This setup call can be removed once proper lifecycle management is implemented, for now this also adds a nice way to reconnect the controller
                {
                    MessageBox.Show("No midi device detected. Are you sure your device is plugged in correctly?");
                    return;
                }
            }
                       
            foreach (var fader in midiDevice.faders)
            {
                MenuItem faderMenu = new MenuItem($@"Fader {fader.faderNumber + 1} - " + getProgramLabel(fader));
                trayMenu.MenuItems.Add(faderMenu);

                // Add master mixerSession to menu
                foreach(MixerSession mixerSession in masterMixerSessionList)
                {
                    MenuItem masterItem = new MenuItem(mixerSession.label, AssignFader);
                    masterItem.Tag = new object[] { fader, mixerSession };
                    faderMenu.MenuItems.Add(masterItem);
                }

                faderMenu.MenuItems.Add("-");

                // Add focus mixerSession to menu                
                MenuItem focusItem = new MenuItem(focusMixerSession.label, AssignFader);
                focusItem.Tag = new object[] { fader, focusMixerSession };
                faderMenu.MenuItems.Add(focusItem);

                faderMenu.MenuItems.Add("-");

                // Add application mixer sessions to each fader
                foreach (var mixerSession in mixerSessions)
                {
                    MenuItem si = new MenuItem(mixerSession.label, AssignFader);
                    si.Tag = new object[] { fader, mixerSession };
                    faderMenu.MenuItems.Add(si);
                }

                faderMenu.MenuItems.Add("-");

                // Add unassign option
                MenuItem unassignItem = new MenuItem("Unassign", UnassignFader);
                unassignItem.Tag = new object[] { fader };
                faderMenu.MenuItems.Add(unassignItem);                
            }

            trayMenu.MenuItems.Add("Exit", OnExit);
        }

        private void AssignFader(object sender, EventArgs e)
        {
            var fader = (Fader)((object[])((MenuItem)sender).Tag)[0];
            var mixerSession = (MixerSession)((object[])((MenuItem)sender).Tag)[1];
            fader.Assign(mixerSession);
            midiDevice.SaveAssignments();
        }

        private void UnassignFader(object sender, EventArgs e)
        {
            var fader = (Fader)((object[])((MenuItem)sender).Tag)[0];
            fader.Unassign();
            midiDevice.SaveAssignments();
        }

        protected override void OnLoad(EventArgs e)
        {
            Visible = false; // Hide form window.
            ShowInTaskbar = false; // Remove from taskbar.

            base.OnLoad(e);
        }

        private void OnExit(object sender, EventArgs e)
        {
            midiDevice.ResetAllLights();
            Application.Exit();
        }

    }
}
