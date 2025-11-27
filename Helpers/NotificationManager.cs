using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SistemaOficina.Helpers
{
    public enum NotificationType
    {
        Success,
        Warning,
        Error
    }

    public static class NotificationManager
    {
        private static Panel _notificationPanel;
        private static readonly List<Border> ActiveNotifications = new List<Border>();

        public static void Initialize(Panel notificationPanel)
        {
            _notificationPanel = notificationPanel;
        }

        public static void Show(string message, NotificationType type = NotificationType.Success, int durationInSeconds = 5)
        {
            if (_notificationPanel == null)
            {
                return;
            }

            var notification = CreateNotificationVisual(message, type);
            AddNotificationToPanel(notification);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(durationInSeconds) };
            timer.Tag = notification;
            timer.Tick += (sender, args) =>
            {
                if (timer.Tag is Border b)
                {
                    RemoveNotificationFromPanel(b);
                }
                timer.Stop();
            };
            timer.Start();
        }

        private static Border CreateNotificationVisual(string message, NotificationType type)
        {
            var iconText = "";
            var backgroundColor = Brushes.Transparent;
            var borderColor = Brushes.Transparent;

            switch (type)
            {
                case NotificationType.Success:
                    iconText = "\uE73E"; // Checkmark icon from Segoe MDL2 Assets
                    backgroundColor = new SolidColorBrush(Color.FromRgb(223, 240, 216));
                    borderColor = new SolidColorBrush(Color.FromRgb(214, 233, 198));
                    break;
                case NotificationType.Warning:
                    iconText = "\uE783"; // Warning icon
                    backgroundColor = new SolidColorBrush(Color.FromRgb(252, 248, 227));
                    borderColor = new SolidColorBrush(Color.FromRgb(250, 235, 204));
                    break;
                case NotificationType.Error:
                    iconText = "\uE783"; // Error/Cancel icon
                    backgroundColor = new SolidColorBrush(Color.FromRgb(242, 222, 222));
                    borderColor = new SolidColorBrush(Color.FromRgb(235, 204, 204));
                    break;
            }

            var notificationBorder = new Border
            {
                Background = backgroundColor,
                BorderBrush = borderColor,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(0, 0, 0, 10),
                MaxWidth = 350,
                MinWidth = 300,
                Opacity = 0
            };

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconLabel = new TextBlock
            {
                Text = iconText,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 20,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(15, 15, 15, 15)
            };

            var messageLabel = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 15, 30, 15) // Margem para não ficar sob o botão
            };

            var closeButton = new Button
            {
                Content = "\uE711", // Close icon
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(5),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 5, 5, 0), // Ajuste fino da posição
                Cursor = Cursors.Hand
            };
            closeButton.Click += (s, e) => RemoveNotificationFromPanel(notificationBorder);

            Grid.SetColumn(iconLabel, 0);
            Grid.SetColumn(messageLabel, 1);
            // O botão fica na coluna 1 também, mas alinhado à direita
            Grid.SetColumnSpan(closeButton, 2);

            mainGrid.Children.Add(iconLabel);
            mainGrid.Children.Add(messageLabel);
            mainGrid.Children.Add(closeButton);

            notificationBorder.Child = mainGrid;

            return notificationBorder;
        }

        private static void AddNotificationToPanel(Border notification)
        {
            _notificationPanel.Children.Insert(0, notification);
            ActiveNotifications.Add(notification);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            notification.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private static void RemoveNotificationFromPanel(Border notification)
        {
            if (!ActiveNotifications.Contains(notification)) return;

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s, e) => {
                _notificationPanel.Children.Remove(notification);
                ActiveNotifications.Remove(notification);
            };
            notification.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
    }
}
