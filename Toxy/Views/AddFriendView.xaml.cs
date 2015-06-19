﻿using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using SharpTox.Core;
using Toxy.ViewModels;
using Toxy.Managers;

namespace Toxy.Views
{
    /// <summary>
    /// Interaction logic for AddFriendView.xaml
    /// </summary>
    public partial class AddFriendView : UserControl
    {
        public AddFriendView()
        {
            InitializeComponent();
        }

        private async void ButtonAddFriend_Click(object sender, RoutedEventArgs e)
        {
            string id = TextBoxFriendId.Text.Trim();
            string message = TextBoxMessage.Text.Trim();

            if (string.IsNullOrEmpty(id))
            {
                ShowError("The Tox ID field is empty.");
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                ShowError("The message field is empty.");
                return;
            }

            if (!ToxId.IsValid(id))
            {
                ShowError("The entered Tox ID is invalid.");
                return;
            }

            var error = ToxErrorFriendAdd.Ok;
            int friendNumber = ProfileManager.Instance.Tox.AddFriend(new ToxId(id), message, out error);

            if (error != ToxErrorFriendAdd.Ok)
            {
                ShowError(error.ToString());
                return;
            }

            var model = new FriendControlViewModel();
            model.ChatNumber = friendNumber;
            model.Name = id;
            model.StatusMessage = "Friend request sent";

            MainWindow.Instance.ViewModel.CurrentFriendListView.AddObject(model);
            MainWindow.Instance.ViewModel.CurrentFriendListView.SortObject(model);
            MainWindow.Instance.ViewModel.CurrentFriendListView.SelectObject(model);

            await ProfileManager.Instance.SaveAsync();
        }

        private void ShowError(string message)
        {
            MessageBox.Show("Could not send friend request: " + message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
