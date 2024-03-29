﻿using System;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace SBMessenger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = MessengerInterop.UsersMessages;
            this.Show();


            MessengerInterop.MessageResultHandler messageReceivedHandler = delegate ()
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                (ThreadStart)delegate ()
                {
                    string user = MessengerInterop.mRres.UserId;
                   
                    MessengerInterop.Users[user].unreadMesages += 1;
                    SuccessToaster.Toast(message: "Новое сообщение от " + user, animation: netoaster.ToasterAnimation.FadeIn);
                    ICollectionView view = CollectionViewSource.GetDefaultView(MessengerInterop.Users.Values);
                    view.Refresh();
                    if (CurrentUser == user)
                    {
                        markMessagesSeen();
                        MessengerInterop.Users[user].unreadMesages = 0;
                        view = CollectionViewSource.GetDefaultView(MessengerInterop.UsersMessages.Values);
                        view.Refresh();
                    }
                });
            };

            MessengerInterop.MessageResultHandler changedStatusHandler = delegate ()
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                (ThreadStart)delegate ()
                {
                    ICollectionView view = CollectionViewSource.GetDefaultView(MessengerInterop.UsersMessages[CurrentUser]);
                    view.Refresh();
                });
            };

            MessengerInterop.MessageResultHandler usersRequestHandler = delegate ()
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                (ThreadStart)delegate ()
                {
                    UsersList.ItemsSource = MessengerInterop.Users.Values;
                    usersCounter.Text = MessengerInterop.Users.Count + " пользователей онлайн";
                    CurrentUser = MessengerInterop.UserName;
                });
            };
            MessengerInterop.mRres.MessageReceivedEvent += messageReceivedHandler;
            MessengerInterop.stCres.StatusChangedEvent += changedStatusHandler;
            MessengerInterop.urh.UsersChangedEvent += usersRequestHandler;
            getUser();



        }
        void getUser()
        {
            Credentials creds = SQLiteConnector.checkCredentials();
            if (creds == null)
            {
                showDialog();
            }
            else
            {
                MessengerInterop.Init(creds.Url, (ushort)creds.Port);
                Task<OperationResult> task = MessengerInterop.Login(creds.Login, creds.Password);

                switch (task.Result)
                {
                    case OperationResult.Ok:
                        //SuccessToaster.Toast(message: "Успех", animation: netoaster.ToasterAnimation.FadeIn);
                        MessengerInterop.RegisterObserver();
                        MessengerInterop.RequestActiveUsers();
                        break;
                    case OperationResult.AuthError: ErrorToaster.Toast(message: "AuthError"); break;
                    case OperationResult.NetworkError:
                        {
                            ErrorToaster.Toast(message: "NetworkError");
                            MessengerInterop.Disconnect();
                            showDialog();
                            break;
                        }
                    case OperationResult.InternalError: ErrorToaster.Toast(message: "InternalError"); break;
                }
            }
        }
        void showDialog()
        {

            LoginWindow aboutWindow = new LoginWindow();
            aboutWindow.Owner = this;
            aboutWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            aboutWindow.ShowDialog();

        }
        string CurrentUser = "";
        //Message CurrentMessage;

        private void UsersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                CurrentUser = ((User)e.AddedItems[0]).UserID;
            }
            UserName.Content = CurrentUser;
            markMessagesSeen();
            MessengerInterop.Users[CurrentUser].unreadMesages = 0;
            ICollectionView view = CollectionViewSource.GetDefaultView(MessengerInterop.Users.Values);
            view.Refresh();
            MessagesLV.ItemsSource = MessengerInterop.UsersMessages[CurrentUser];
        }

        private void markMessagesSeen()
        {
            for (int i = 0; i < MessengerInterop.Users[CurrentUser].unreadMesages; i++)
            {
                int count = MessengerInterop.UsersMessages[CurrentUser].Count;
                string ReceivedMessageId = MessengerInterop.UsersMessages[CurrentUser][count - i - 1].MessageId;
                MessengerInterop.SendMessageSeen(CurrentUser, ReceivedMessageId);
            }
        }

        private void SendMessageButton_Click(object sender, RoutedEventArgs e)
        {
            string msg = MessageText.Text;
            if (msg.Length > 0)
            {
                byte[] mesg = Encoding.UTF8.GetBytes(msg + '\0');
                MessengerInterop.CurrentMessage = new Message(MessengerInterop.UserName, msg, DateTime.Now);
                MessengerInterop.SendComplexMessage(CurrentUser, MessageContentType.Text, false, mesg, mesg.Length);

                MessengerInterop.UsersMessages[CurrentUser].Add(MessengerInterop.CurrentMessage);
                SQLiteConnector.AddMessage(MessengerInterop.CurrentMessage, MessengerInterop.UserName);
                ICollectionView view = CollectionViewSource.GetDefaultView(MessengerInterop.UsersMessages[CurrentUser]);
                view.Refresh();
                MessageText.Text = "";
            }
            else
            {
                ErrorToaster.Toast(message: "Нельзя отправлять пустые сообщения", animation: netoaster.ToasterAnimation.FadeIn);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            MessengerInterop.Disconnect();
            base.OnClosed(e);
        }

        private void ResreshButton_Click(object sender, RoutedEventArgs e)
        {
            MessengerInterop.RequestActiveUsers();
        }

        private void QuitButton_Click(object sender, RoutedEventArgs e)
        {
            MessengerInterop.Disconnect();
            showDialog();
        }
    }
}
