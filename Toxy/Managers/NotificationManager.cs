﻿using SharpTox.Av;
using SharpTox.Core;
using Toxy.Extensions;

namespace Toxy.Managers
{
    public class NotificationManager : IToxManager
    {
        private Tox _tox;
        private ToxAv _toxAv;

        public void SwitchProfile(Tox tox, ToxAv toxAv)
        {
            _tox = tox;
            _toxAv = toxAv;

            _tox.OnFriendMessageReceived += Tox_OnFriendMessageReceived;
            _tox.OnGroupMessage += Tox_OnGroupMessage;
            _tox.OnGroupAction += Tox_OnGroupAction;
        }

        private void Tox_OnGroupMessage(object sender, ToxEventArgs.GroupMessageEventArgs e)
        {
            if (!Config.Instance.EnableFlashOnGroupMessage)
                return;

            var window = MainWindow.Instance;

            MainWindow.Instance.UInvoke(() =>
            {
                if (!window.IsActive || window.WindowState == System.Windows.WindowState.Minimized)
                {
                    window.Flash();
                }
            });
        }

        private void Tox_OnGroupAction(object sender, ToxEventArgs.GroupActionEventArgs e)
        {
            Tox_OnGroupMessage(sender, new ToxEventArgs.GroupMessageEventArgs(e.GroupNumber, e.PeerNumber, e.Action));
        }

        private void Tox_OnFriendMessageReceived(object sender, ToxEventArgs.FriendMessageEventArgs e)
        {
            if (!Config.Instance.EnableFlashOnFriendMessage || Config.Instance.NotificationBlacklist.Contains(ProfileManager.Instance.Tox.GetFriendPublicKey(e.FriendNumber).ToString()))
                return;

            var window = MainWindow.Instance;

            window.UInvoke(() =>
            {
                if (!window.IsActive || window.WindowState == System.Windows.WindowState.Minimized)
                {
                    window.Flash();
                }
            });
        }
    }
}
