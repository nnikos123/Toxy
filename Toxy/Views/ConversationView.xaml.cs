﻿using System;
using System.Windows.Controls;
using SharpTox.Core;
using SharpTox.Av;
using Toxy.ViewModels;
using Toxy.Extensions;
using System.Windows.Input;
using Toxy.Managers;
using Microsoft.Win32;
using System.Windows;
using System.Threading;
using ShareX.ScreenCaptureLib;
using System.IO;
using System.Drawing.Imaging;

namespace Toxy.Views
{
    /// <summary>
    /// Interaction logic for ConversationView.xaml
    /// </summary>
    public partial class ConversationView : UserControl
    {
        private bool _autoScroll;
        private Timer _typingTimer;

        public ConversationViewModel Context { get { return DataContext as ConversationViewModel; } }

        public ConversationView()
        {
            InitializeComponent();

            _typingTimer = new Timer((s) => 
            {
                MainWindow.Instance.UInvoke(() => 
                {
                    if (Context != null) 
                        Context.Friend.SetSelfTypingStatus(false);
                });
            }, null, Timeout.Infinite, Timeout.Infinite);
        }

        private void TextBoxEnteredText_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var text = TextBoxEnteredText.Text;

            if (e.Key == Key.Enter)
            {
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                    return;

                if (e.IsRepeat)
                    return;

                SendMessage(text);
                e.Handled = true;
            }
        }

        private void ButtonSendMessage_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            SendMessage(TextBoxEnteredText.Text);
        }

        private void SendMessage(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var chatNumber = Context.Friend.ChatNumber;
            if (!ProfileManager.Instance.Tox.IsFriendOnline(chatNumber))
                return;

            var model = new MessageViewModel(-1);
            model.FriendName = ProfileManager.Instance.Tox.Name;
            model.Time = DateTime.Now.ToShortTimeString();

            if (text.StartsWith("/me "))
            {
                //action
                string action = text.Substring(4);
                int messageid = ProfileManager.Instance.Tox.SendMessage(chatNumber, action, ToxMessageType.Action);

                model.Message = action;
                model.MessageId = messageid;
                Context.AddMessage(model);
            }
            else
            {
                //regular message
                //foreach (string message in text.WordWrap(ToxConstants.MaxMessageLength))
                //{
                int messageid = ProfileManager.Instance.Tox.SendMessage(chatNumber, text, ToxMessageType.Message);


                model.Message = text;
                model.MessageId = messageid;
                Context.AddMessage(model);
                //}
            }

            //ScrollChatBox();
            TextBoxEnteredText.Text = string.Empty;
        }

        private void Call_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if ((Context.Friend.CallState & CallState.Calling) != 0)
            {
                //answer the call
                if (ProfileManager.Instance.CallManager.Answer(Context.Friend.ChatNumber, false))
                {
                    if ((Context.Friend.CallState & CallState.SendingVideo) != 0)
                        Context.Friend.CallState = CallState.InProgress | CallState.SendingVideo;
                    else
                        Context.Friend.CallState = CallState.InProgress;
                }
            }
            else if ((Context.Friend.CallState & CallState.InProgress) != 0 || (Context.Friend.CallState & CallState.Ringing) != 0)
            {
                //hang up
                if (ProfileManager.Instance.CallManager.Hangup(Context.Friend.ChatNumber))
                {
                    Context.Friend.CallState = CallState.None;
                }
            }
            else
            {
                //send call request
                if (ProfileManager.Instance.CallManager.SendRequest(Context.Friend.ChatNumber, false))
                {
                    Context.Friend.CallState = CallState.Ringing;
                }
            }
        }

        private void ButtonVideo_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if ((Context.Friend.CallState & CallState.Calling) != 0)
            {
                //answer the call
                if (ProfileManager.Instance.CallManager.Answer(Context.Friend.ChatNumber, true))
                {
                    Context.Friend.CallState = CallState.ReceivingVideo | CallState.InProgress;

                    if ((Context.Friend.CallState & CallState.SendingVideo) != 0)
                        Context.Friend.CallState |= CallState.SendingVideo;
                }
            }
            else if ((Context.Friend.CallState & CallState.InProgress) != 0)
            {
                //toggle video
                ProfileManager.Instance.CallManager.ToggleVideo(!Context.Friend.CallState.HasFlag(CallState.ReceivingVideo));
                Context.Friend.CallState ^= CallState.ReceivingVideo;
            }
            else if ((Context.Friend.CallState & CallState.Ringing) == 0)
            {
                //send call request
                if (ProfileManager.Instance.CallManager.SendRequest(Context.Friend.ChatNumber, true))
                {
                    Context.Friend.CallState = CallState.Ringing | CallState.ReceivingVideo;
                }
            }
        }

        private void ScrollbackViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null)
                return;

            if (e.ExtentHeightChange == 0)
                _autoScroll = scrollViewer.VerticalOffset == scrollViewer.ScrollableHeight;

            if (_autoScroll && e.ExtentHeightChange != 0)
                scrollViewer.ScrollToVerticalOffset(scrollViewer.ExtentHeight);
        }

        private void ButtonSendFile_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Multiselect = false;
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (dialog.ShowDialog() != true)
                return;

            var transfer = ProfileManager.Instance.TransferManager.SendFile(Context.Friend.ChatNumber, dialog.FileName);
            if (transfer == null)
            {
                MessageBox.Show("Could not send file transfer request.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            (Context.Friend.ConversationView as ConversationViewModel).AddTransfer(transfer);
        }

        private void TextBoxEnteredText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!Config.Instance.SendTypingNotifications)
                return;

            _typingTimer.Change(1000, -1);

            if (!Context.Friend.SelfIsTyping)
            {
                Context.Friend.SetSelfTypingStatus(true);
            }
        }

        private void ButtonSendScreenshot_Click(object sender, RoutedEventArgs e)
        {
            using (var screenshot = Screenshot.CaptureFullscreen())
            using (var surface = new RectangleRegion())
            {
                surface.SurfaceImage = screenshot;
                surface.Prepare();
                surface.ShowDialog();

                using (var img = surface.GetRegionImage())
                {
                    if (img == null)
                        return;

                    var stream = new MemoryStream();
                    img.Save(stream, ImageFormat.Png);

                    var transfer = ProfileManager.Instance.TransferManager.SendFile(Context.Friend.ChatNumber, stream, "screenshot.png");
                    if (transfer == null)
                    {
                        MessageBox.Show("Could not send screenshot.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    (Context.Friend.ConversationView as ConversationViewModel).AddTransfer(transfer);
                }
            }
        }
    }
}
